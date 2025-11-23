using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Gemini;
using DocTask.Core.Dtos.SubTasks;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Dtos.UploadFile;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Data;
using DocTask.Service.Helpers;
using DocTask.Service.Mappers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using static DocTask.Core.Dtos.Gemini.GeminiDto;

namespace DocTask.Service.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _geminiApiKey;
        private readonly IUploadFileRepository _uploadFileRepository;
        private readonly IUploadFileService _uploadFileService;
        private readonly ITaskRepository _taskRepository;
        private readonly IProgressRepository _progressRepository;
        private readonly ApplicationDbContext _context;
        private readonly IFileConvertService _fileConvertService;
        private readonly IAgentRepository _agentRepository;
        private readonly IMemoryCache _memoryCache;
        private const int FileContextChunkSize = 3000;

        public GeminiService(
            HttpClient httpClient,
            GeminiDto.GeminiOptions options,
            IUploadFileRepository uploadFileRepository,
            IUploadFileService uploadFileService,
            ITaskRepository taskRepository,
            IProgressRepository progressRepository,
            ApplicationDbContext context,
            IFileConvertService fileConvertService,
            IMemoryCache memoryCache,
            IAgentRepository agentRepository
        )
        {
            _httpClient = httpClient;
            _geminiApiKey = options.ApiKey;
            _uploadFileRepository = uploadFileRepository;
            _uploadFileService = uploadFileService;
            _taskRepository = taskRepository;
            _progressRepository = progressRepository;
            _context = context;
            _fileConvertService = fileConvertService;
            _memoryCache = memoryCache;
            _agentRepository = agentRepository;
        }

        public async Task<ChatResponse> AskWithFileAsync(int fileId, bool redo = false)
        {
            var cacheKey = redo ? $"{GetCacheKey(fileId)}_redo_{Guid.NewGuid()}" : GetCacheKey(fileId);

            if (!redo && _memoryCache.TryGetValue(cacheKey, out ChatResponse cachedResponse))
            {
                return cachedResponse;
            }

            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null)
                throw new ArgumentException("File not found");

            var fileContent = await _fileConvertService.GetFileContentAsync(file.FilePath);

            // Chia nội dung file thành các chunk nhỏ (~4000-5000 ký tự mỗi chunk)
            var chunks = SplitIntoChunks(fileContent, FileContextChunkSize);

            var additionalContext = new Dictionary<string, string>
            {
                ["Ngày hiện tại"] = DateTime.Now.ToString("dd/MM/yyyy"),
                ["Ngày kết thúc mặc định"] = DateTime.Now.AddDays(45).ToString("dd/MM/yyyy"),
                ["Tên file"] = file.FileName
            };

            var chunkResponses = new List<JsonElement>();

            // Gọi Gemini cho từng chunk
            foreach (var chunk in chunks)
            {
                var combinedMessage = redo
                    ? $"[RETRY_ID: {Guid.NewGuid()}]\n[FILE CONTENT CHUNK]\n{chunk}\n\n"
                    : $"[FILE CONTENT CHUNK]\n{chunk}\n\n";

                var responseText = await AskPlanAsync(combinedMessage, PromptContextType.GenerateTasks, additionalContext, temperature: redo ? 0.75 : 0.0) as string;

                if (!string.IsNullOrWhiteSpace(responseText))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(responseText);
                        chunkResponses.Add(doc.RootElement.Clone());
                    }
                    catch
                    {
                        Console.WriteLine("Chunk JSON parse failed, skipping chunk.");
                    }
                }
            }

            // Merge các chunk JSON lại
            var merged = MergeChunkResponses(chunkResponses);

            var response = new ChatResponse
            {
                Response = JsonSerializer.Serialize(merged, new JsonSerializerOptions { WriteIndented = true })
            };

            _memoryCache.Set(cacheKey, response, TimeSpan.FromMinutes(30));
            Console.WriteLine(_memoryCache.TryGetValue(cacheKey, out _) ? " Đã lưu vào cache" : " Không lưu được vào cache");
            return response;
        }

        public Task<ChatResponse?> GetPreviewAsync(int fileId)
        {
            var cacheKey = GetCacheKey(fileId);
            _memoryCache.TryGetValue(cacheKey, out ChatResponse? cachedResponse);
            return System.Threading.Tasks.Task.FromResult(cachedResponse);
        }

        public bool RejectPlan(int fileId)
        {
            var cacheKey = GetCacheKey(fileId);
            if (_memoryCache.TryGetValue(cacheKey, out _))
            {
                _memoryCache.Remove(cacheKey);
                return true;
            }

            return false;
        }

        public async Task<(byte[] fileContent, string fileName, string contentType)> AskWithTaskSummaryAsync(int taskId, int userId, string format)
        {
            // Lấy thông tin task để biết frequency
            var taskModel = await _taskRepository.GetTaskByIdAsync(taskId);
            var task = await _context.Tasks
                .IgnoreQueryFilters() // loại bỏ global filters nếu có
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
            Console.WriteLine(task == null ? "Task not found" : "Task found");

            if (taskModel == null)
            {
                Console.WriteLine($"[DEBUG] Task {taskId} not found via repository");
                var taskDirect = await _context.Tasks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TaskId == taskId);
                Console.WriteLine(taskDirect != null ? $"[DEBUG] Task {taskId} found directly in context" : "Task really not found");

                throw new ArgumentException("Task not found");
            }

            var frequencyType = taskModel.Frequency?.FrequencyType?.Trim().ToLower() ?? "daily";
            var taskName = taskModel.Title ?? "Unknown Task";

            // Lấy 10 báo cáo mới nhất của task này
            var progresses = await _progressRepository.GetProgressesByTaskAsync(taskId);
            var latestProgresses = progresses
                .OrderByDescending(p => p.UpdatedAt)
                .Take(10)
                .ToList();

            if (!latestProgresses.Any())
            {
                throw new ArgumentException("Chưa có báo cáo nào cho task này");
            }

            // Nhóm báo cáo theo kỳ (period) và user
            var groupedData = new List<object>();

            foreach (var progress in latestProgresses)
            {
                string fullText = string.Empty;
                int? fileId = null;

                if (!string.IsNullOrEmpty(progress.FilePath))
                {
                    // Join sang UploadFile, đọc file bên upload
                    var file1 = await _context.Uploadfiles
                        .Where(f => f.FilePath == progress.FilePath || f.FileName == progress.FileName)
                        .FirstOrDefaultAsync();

                    if (file1 != null)
                    {
                        fileId = file1.FileId;
                        try
                        {
                            using var stream = await _uploadFileService.DownloadFileAsync(file1.FileId);
                            if (stream != null)
                            {
                                fullText = await _fileConvertService.GetFileContentAsync(file1.FilePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            fullText = $"[Lỗi đọc file {file1.FileName}: {ex.Message}]";
                        }
                    }
                }

                groupedData.Add(new
                {
                    progressId = progress.ProgressId,
                    period = GetPeriodKey(progress.UpdatedAt, frequencyType),
                    userName = progress.UpdatedByFullName ?? progress.UpdatedByUserName ?? "Unknown User",
                    userId = progress.UpdatedBy,
                    updatedAt = progress.UpdatedAt,
                    status = progress.Status,
                    proposal = progress.Proposal,
                    result = progress.Result,
                    feedback = progress.Feedback,
                    fileId = fileId,
                    fileName = progress.FileName,
                    filePath = progress.FilePath,
                    fullText = fullText
                });
            }

            var jsonData = JsonSerializer.Serialize(groupedData, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            var userMessage = $@"
                **Dữ liệu báo cáo (10 bản ghi gần nhất):**
                {jsonData}

                **Yêu cầu:** Hãy tổng hợp báo cáo theo cấu trúc đã định nghĩa, ưu tiên phân tích từ fullText.";

            // Additional context
            var additionalContext = new Dictionary<string, string>
            {
                { "Task ID", taskId.ToString() },
                { "Tên Task", taskName },
                { "Loại báo cáo", frequencyType },
                { "Số báo cáo", latestProgresses.Count.ToString() }
            };

            var summary = await AskSummaryAsync(
                userMessage,
                PromptContextType.TaskSummary,
                additionalContext
            );

            var response = summary.ToString();
            var extension = format.ToLower() switch
            {
                "pdf" => ".pdf",
                "doc" or "docx" or "word" => ".docx",
                "txt" or "text" => ".txt",
                "xls" or "xlsx" or "excel" => ".xlsx",
                _ => ".pdf"
            };

            var fileName = $"Báo Cáo Tổng Hợp_{DateTime.UtcNow:yyyy-MM-dd_HHmmssZ}{extension}";
            var (fileContent, contentType) = await _fileConvertService.ConvertFileFormatAsync(response, fileName, format);

            using var ms = new MemoryStream(fileContent);
            var file = new FormFile(ms, 0, fileContent.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            var uploadRequest = new UploadFileRequest
            {
                File = file,
                Description = $"Báo cáo tổng hợp_{DateTime.Now:yyyyMMdd}",
            };

            var uploadResult = await _uploadFileService.UploadFileAsync(uploadRequest, userId);

            return (fileContent, fileName, contentType);
        }

        public async Task<object> AskAsync(
            string userMessage,
            string systemPromptTemplate,
            PromptContextType contextType = PromptContextType.GeneralChat,
            Dictionary<string, string>? additionalContext = null,
            double temperature = 0.0
        )
        {
            // Build system prompt
            var systemPrompt = systemPromptTemplate
                .Replace("{DATA_SCHEMA}", GeminiPrompts.GetDataSchema(contextType))
                .Replace("{TASK_DESCRIPTION}", GeminiPrompts.GetTaskDescription(contextType))
                .Replace("{REMOVE_CLUTTER}", GeminiPrompts.GetRemoveClutter(contextType))
                .Replace("{OUTPUT_FORMAT}", GeminiPrompts.GetOutputFormat(contextType));

            if (additionalContext != null && additionalContext.Any())
            {
                var contextStr = string.Join("\n", additionalContext.Select(kv => $"**{kv.Key}:** {kv.Value}"));
                systemPrompt += $"\n\n**Thông tin bổ sung:**\n{contextStr}";
            }

            var requestBody = new
            {
                contents = new object[]
                {
                    new
                    {
                        role = "user",
                        parts = new object[] { new { text = PromptHelper.Clean(systemPrompt) } }
                    },
                    new
                    {
                        role = "user",
                        parts = new object[] { new { text = PromptHelper.Clean(userMessage) } }
                    },
                },
                generationConfig = new
                {
                    temperature = 0.75,
                    candidateCount = 3,
                    topP = 0.8,
                    topK = 40,
                    maxOutputTokens = 4096,
                }
            };

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiApiKey}";

            var httpResponse = await _httpClient.PostAsJsonAsync(url, requestBody);
            var raw = await httpResponse.Content.ReadAsStringAsync();

            int maxRetries = 3;
            int delayMs = 2500;
            string? rawResponse = null;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Timeout mỗi request 30s

                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = JsonContent.Create(requestBody)
                    };
                    request.Headers.Add("User-Agent", "DocTaskAI/1.0");
                    request.Headers.Add("Accept", "application/json");

                    var response = await _httpClient.SendAsync(request, cts.Token);
                    rawResponse = await response.Content.ReadAsStringAsync();

                    if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                    {
                        Console.WriteLine($"Gemini bị 503 (lần {i + 1}/{maxRetries}). Thử lại sau {delayMs}ms...");
                        await System.Threading.Tasks.Task.Delay(delayMs);
                        delayMs *= 2;
                        continue;
                    }

                    response.EnsureSuccessStatusCode();
                    break;
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($" Lỗi mạng khi gọi Gemini: {ex.Message}");
                    if (i == maxRetries - 1) throw;
                    await System.Threading.Tasks.Task.Delay(delayMs);
                    delayMs *= 2;
                }
            }

            if (string.IsNullOrWhiteSpace(rawResponse))
                return "Không nhận được phản hồi từ Gemini.";


            Console.WriteLine("==== RAW Gemini ====");
            Console.WriteLine(raw);

            httpResponse.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(raw);
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            if (string.IsNullOrWhiteSpace(text))
                return "No response text";

            // Clean code block
            var clean = text.Replace("```json", "")
                      .Replace("```", "")
                      .Trim();

            // Parse JSON chuẩn
            if (clean.StartsWith("{") || clean.StartsWith("["))
            {
                try
                {
                    using var parsed = JsonDocument.Parse(clean);
                    return parsed.RootElement.Clone();
                }
                catch { /* ignore */ }
            }

            // Parse JSON escaped trong chuỗi
            try
            {
                var unescaped = JsonSerializer.Deserialize<string>(clean);
                if (!string.IsNullOrEmpty(unescaped) &&
                    (unescaped.TrimStart().StartsWith("{") || unescaped.TrimStart().StartsWith("[")))
                {
                    using var parsed = JsonDocument.Parse(unescaped);
                    return parsed.RootElement.Clone();
                }
            }
            catch { /* ignore */ }

            // Trả về text thường
            return clean;
        }

        public async Task<string> AskPlanAsync(
            string userMessage,
            PromptContextType contextType = PromptContextType.GeneralChat,
            Dictionary<string, string>? additionalContext = null,
            double temperature = 0.0
        )
        {
            var result = await AskAsync(userMessage, GeminiPrompts.MasterPlanPrompt, contextType, additionalContext, temperature);

            // Convert object thành string
            if (result is JsonElement jsonElement)
            {
                return jsonElement.GetRawText(); // trả JSON dạng chuỗi
            }

            return result?.ToString() ?? string.Empty;
        }

        private string GetCacheKey(int fileId)
        {
            return $"file_{fileId}";
        }

        public Task<object> AskSummaryAsync(
            string userMessage,
            PromptContextType contextType = PromptContextType.GeneralChat,
            Dictionary<string, string>? additionalContext = null
        )
        {
            return AskAsync(
                userMessage,
                GeminiPrompts.MasterSummaryPrompt,
                contextType: contextType,
                additionalContext: additionalContext
            );
        }

        // Helper method (có thể tái sử dụng từ ProgressService)
        private string GetPeriodKey(DateTime date, string frequencyType)
        {
            return frequencyType switch
            {
                "daily" => date.ToString("yyyy-MM-dd"),
                "weekly" => $"Tuần {GetWeekOfYear(date)} - {date.Year}",
                "monthly" => date.ToString("yyyy-MM"),
                _ => date.ToString("yyyy-MM-dd")
            };
        }

        private int GetWeekOfYear(DateTime date)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            return culture.Calendar.GetWeekOfYear(date,
                System.Globalization.CalendarWeekRule.FirstDay,
                DayOfWeek.Monday);
        }
        
        // Helper: chia nội dung thành chunk
        private static List<string> SplitIntoChunks(string text, int size)
        {
            var chunks = new List<string>();
            if (string.IsNullOrEmpty(text) || size <= 0)
            {
                return chunks;
            }

            for (int i = 0; i < text.Length; i += size)
            {
                chunks.Add(text.Substring(i, Math.Min(size, text.Length - i)));
            }

            return chunks;
        }

        // Helper: merge các chunk JSON thành 1
        private object MergeChunkResponses(List<JsonElement> chunkResponses)
        {
            if (chunkResponses.Count == 0) return new { subtasks = Array.Empty<object>() };

            // Lấy title và description từ chunk đầu tiên
            var firstChunk = chunkResponses[0];
            string title = firstChunk.GetProperty("title").GetString() ?? "";
            string description = firstChunk.GetProperty("description").GetString() ?? "";

            var mergedSubtasks = new List<JsonElement>();
            DateTime? startDate = null;
            DateTime? endDate = null;

            foreach (var chunk in chunkResponses)
            {
                if (chunk.TryGetProperty("subtasks", out var subtasks) && subtasks.ValueKind == JsonValueKind.Array)
                {
                    mergedSubtasks.AddRange(subtasks.EnumerateArray());
                }

                if (chunk.TryGetProperty("startDate", out var s) && DateTime.TryParse(s.GetString(), out var sd))
                {
                    startDate = startDate == null || sd < startDate ? sd : startDate;
                }

                if (chunk.TryGetProperty("endDate", out var e) && DateTime.TryParse(e.GetString(), out var ed))
                {
                    endDate = endDate == null || ed > endDate ? ed : endDate;
                }
            }

            return new
            {
                title,
                description,
                startDate = startDate?.ToString("dd/MM/yyyy") ?? "",
                endDate = endDate?.ToString("dd/MM/yyyy") ?? "",
                subtasks = mergedSubtasks
            };
        }

        public async Task<AgentDto?> CreateAsync(CreateAgentDto createAgentDto)
        {
            var createAgent = new AgentContext
            {
                ContextName = createAgentDto.ContextName,
                ContextDescription = createAgentDto.ContextDescription,
                FileId = createAgentDto.FileId,
            };

            var created = await _agentRepository.CreateAsync(createAgent);
            return created.ToAgentDto();
        }

        public async Task<AgentDto?> GetByIdAsync(int FileId)
        {
            var fileId = await _agentRepository.GetByFileIdAsync(FileId);
            if (fileId == null)
            {
                return null;
            }

            return fileId.ToAgentDto();
        }
    }
}
