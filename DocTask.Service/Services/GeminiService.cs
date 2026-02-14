using System.Globalization;
using System.Text.Json;
using DocTask.Core.Dtos.Gemini;
using DocTask.Core.Dtos.UploadFile;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Data;
using DocTask.Service.Helpers;
using DocTask.Service.Mappers;
using DocTask.Service.Services.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using static DocTask.Core.Dtos.Gemini.AiProviderDto;
using static DocTask.Core.Dtos.Gemini.GeminiDto;

namespace DocTask.Service.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly AiProviderFactory _aiProviderFactory;
        private readonly IUploadFileRepository _uploadFileRepository;
        private readonly IUploadFileService _uploadFileService;
        private readonly ITaskRepository _taskRepository;
        private readonly IProgressRepository _progressRepository;
        private readonly ApplicationDbContext _context;
        private readonly IFileConvertService _fileConvertService;
        private readonly IAgentRepository _agentRepository;
        private readonly IMemoryCache _memoryCache;
        private readonly ITaskDraftService _taskDraftService;
        private const int FileContextChunkSize = 3000;

        /// <summary>
        /// Constructor — inject AiProviderFactory thay vì HttpClient trực tiếp.
        /// Factory tự động xử lý fallback: Ollama → Gemini → Rule-based.
        /// </summary>
        public GeminiService(
            AiProviderFactory aiProviderFactory,
            IUploadFileRepository uploadFileRepository,
            IUploadFileService uploadFileService,
            ITaskRepository taskRepository,
            IProgressRepository progressRepository,
            ApplicationDbContext context,
            IFileConvertService fileConvertService,
            IMemoryCache memoryCache,
            IAgentRepository agentRepository,
            ITaskDraftService taskDraftService
        )
        {
            _aiProviderFactory = aiProviderFactory;
            _uploadFileRepository = uploadFileRepository;
            _uploadFileService = uploadFileService;
            _taskRepository = taskRepository;
            _progressRepository = progressRepository;
            _context = context;
            _fileConvertService = fileConvertService;
            _memoryCache = memoryCache;
            _agentRepository = agentRepository;
            _taskDraftService = taskDraftService;
        }

        public async Task<ChatResponse> AskWithFileAsync(int fileId, int userId, bool redo = false)
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

        // ⭐ LAYER 1: Extract project timeline bằng regex (rule-based) TRƯỚC khi gọi AI
        var (projectStartDate, projectEndDate) = TimelineExtractor.ExtractProjectTimeline(fileContent);

        Console.WriteLine($"[GeminiService] File Content Length: {fileContent.Length}");
        if (fileContent.Length > 0)
        {
            Console.WriteLine($"[GeminiService] Preview (first 500 chars):\n{fileContent.Substring(0, Math.Min(500, fileContent.Length))}\n---END PREVIEW---");
        }

        // ⭐ LAYER 1.5: Rule-Based Template Extraction (bypass AI if modules found)
        var projectInfo = ModuleExtractor.ExtractProjectInfo(fileContent);
        Console.WriteLine($"[GeminiService] Extracted Modules Count: {projectInfo.Modules.Count}");

        if (projectInfo.Modules.Any())
        {
            Console.WriteLine($"✅ [GeminiService] Detected {projectInfo.Modules.Count} modules entirely via Template.");
            
            var pStart = projectStartDate ?? DateTime.Now;
            var pEnd = projectEndDate ?? DateTime.Now.AddDays(30);

            var aiTask = new GeminiTaskDto
            {
                Title = !string.IsNullOrWhiteSpace(projectInfo.Title) ? projectInfo.Title : "Dự án mới",
                Description = !string.IsNullOrWhiteSpace(projectInfo.Description) ? projectInfo.Description : "Được trích xuất từ template tài liệu.",
                StartDate = pStart,
                EndDate = pEnd,
                EstimatedHours = projectInfo.Modules.Sum(m => m.Hours),
                Subtasks = new List<GeminiSubtaskDto>()
            };

            // Distribute timeline sequentially based on hours
            var totalHours = (double)aiTask.EstimatedHours;
            if (totalHours <= 0) totalHours = 1; // avoid div by zero

            var totalDays = (pEnd - pStart).TotalDays;
            var currentStart = pStart;

            foreach (var m in projectInfo.Modules)
            {
                var ratio = m.Hours / totalHours;
                var durationDays = totalDays * ratio;
                
                // Ensure at least 1 day
                if (durationDays < 1) durationDays = 1;

                var moduleEnd = currentStart.AddDays(durationDays);
                if (moduleEnd > pEnd) moduleEnd = pEnd;

                var geminiSubtask = new GeminiSubtaskDto
                {
                    Title = m.Name,
                    Description = m.Desc,
                    EstimatedHours = m.Hours,
                    StartDate = currentStart,
                    DueDate = moduleEnd,
                    RequiredSkills = new List<GeminiSkillRequirementDTO>()
                };

                // Create Work Packages for this module
                geminiSubtask.Subtasks = GenerateWorkPackages(m, projectInfo.TechStack, currentStart, moduleEnd);
                
                aiTask.Subtasks.Add(geminiSubtask);

                // Next module starts when this one ends
                currentStart = moduleEnd;
            }

            // Save Draft directly
            var createdDraftId = await _taskDraftService.CreateDraftFromGeminiAsync(aiTask, fileId, userId);

            return new ChatResponse
            {
                Response = $"Đã trích xuất thành công dự án \"{aiTask.Title}\" với {aiTask.Subtasks.Count} module từ tài liệu.",
                DraftId = createdDraftId,
                AiResponse = aiTask
            };
        }


        var chunks = SplitIntoChunks(fileContent, FileContextChunkSize);

        var additionalContext = new Dictionary<string, string>
        {
            ["Ngày hiện tại"] = DateTime.Now.ToString("dd/MM/yyyy"),
            ["Ngày kết thúc mặc định"] = DateTime.Now.AddDays(45).ToString("dd/MM/yyyy"),
            ["Tên file"] = file.FileName
        };

        // ⭐ LAYER 2: Inject extracted timeline vào prompt (nếu có)
        if (projectStartDate != null && projectEndDate != null)
        {
            additionalContext["Thời gian dự án (ĐÃ XÁC NHẬN)"] = TimelineExtractor.FormatForPrompt(projectStartDate, projectEndDate);
            additionalContext["⚠️ PROJECT_START_DATE"] = projectStartDate?.ToString("dd/MM/yyyy");
            additionalContext["⚠️ PROJECT_END_DATE"] = projectEndDate?.ToString("dd/MM/yyyy");
        }

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

        // ⭐ LAYER 3: Merge và override với extracted timeline
        var merged = MergeChunkResponses(chunkResponses, projectStartDate, projectEndDate);

        // Lưu vào Draft thay vì cache
        var draftId = await _taskDraftService.CreateDraftFromGeminiAsync(merged, fileId, userId);

        var response = new ChatResponse
        {
            Response = "Đã tạo bản nháp thành công",
            DraftId = draftId,
            AiResponse = merged // mergerd now returns GeminiTaskDto
        };

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

        /// <summary>
        /// Gọi AI thông qua AiProviderFactory (Ollama → Gemini → Rule-based).
        /// Build prompt từ template, gửi qua factory, parse response.
        /// </summary>
        public async Task<object> AskAsync(
            string userMessage,
            string systemPromptTemplate,
            PromptContextType contextType = PromptContextType.GeneralChat,
            Dictionary<string, string>? additionalContext = null,
            double temperature = 0.0
        )
        {
            // ===== BƯỚC 1: Build system prompt từ template =====
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

            // ===== BƯỚC 2: Tạo request chuẩn cho AiProviderFactory =====
            var aiRequest = new AiCompletionRequest
            {
                Messages = new System.Collections.Generic.List<AiMessage>
                {
                    new AiMessage { Role = "system", Content = PromptHelper.Clean(systemPrompt) },
                    new AiMessage { Role = "user", Content = PromptHelper.Clean(userMessage) }
                },
                Temperature = temperature,
                MaxOutputTokens = 8192,
                TopP = 0.8
            };

            // ===== BƯỚC 3: Gọi AI qua factory (tự động fallback) =====
            var aiResponse = await _aiProviderFactory.CompleteAsync(aiRequest);

            Console.WriteLine($"==== AI Response (via {aiResponse.ProviderUsed}) ====");
            Console.WriteLine(aiResponse.Text?.Length > 500
                ? aiResponse.Text.Substring(0, 500) + "..."
                : aiResponse.Text);

            var text = aiResponse.Text;

            if (string.IsNullOrWhiteSpace(text))
                return "No response text";

            // ===== BƯỚC 4: Clean và parse response =====
            // Loại bỏ markdown code block nếu có
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
                catch { /* ignore, thử cách khác */ }
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
        private GeminiTaskDto MergeChunkResponses(
            List<JsonElement> chunkResponses, 
            DateTime? projectStartDate = null,
            DateTime? projectEndDate = null
        )
        {
            if (chunkResponses.Count == 0) return new GeminiTaskDto();

            // Lấy title và description từ chunk đầu tiên
            var firstChunk = chunkResponses[0];
            
            // Helper: lấy property case-insensitive
            JsonElement? GetProperty(JsonElement element, string name)
            {
                if (element.TryGetProperty(name, out var prop)) return prop;
                if (element.TryGetProperty(char.ToUpper(name[0]) + name.Substring(1), out var propUpper)) return propUpper;
                if (element.TryGetProperty(name.ToUpper(), out var propAllUpper)) return propAllUpper;
                return null;
            }

            string title = "Untitled Task";
            string description = "";

            var titlePropRaw = GetProperty(firstChunk, "title");
            if (titlePropRaw.HasValue) title = titlePropRaw.Value.GetString() ?? title;
            
            var descPropRaw = GetProperty(firstChunk, "description");
            if (descPropRaw.HasValue) description = descPropRaw.Value.GetString() ?? "";

            var mergedSubtasks = new List<JsonElement>();
            DateTime? aiStartDate = null;
            DateTime? aiEndDate = null;
            decimal? estimatedHours = null;
            var mergedSkills = new List<JsonElement>();

            string[] dateFormats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "yyyy/MM/dd" };

            foreach (var chunk in chunkResponses)
            {
                // Merge subtasks
                var subtasksProp = GetProperty(chunk, "subtasks");
                if (subtasksProp.HasValue && subtasksProp.Value.ValueKind == JsonValueKind.Array)
                {
                    mergedSubtasks.AddRange(subtasksProp.Value.EnumerateArray());
                }

                // Lấy startDate sớm nhất từ AI
                var sProp = GetProperty(chunk, "startDate");
                if (sProp.HasValue && sProp.Value.ValueKind == JsonValueKind.String)
                {
                    string sVal = sProp.Value.GetString();
                    if (DateTime.TryParseExact(sVal, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd))
                    {
                        aiStartDate = aiStartDate == null || sd < aiStartDate ? sd : aiStartDate;
                    }
                }

                // Lấy endDate muộn nhất từ AI
                var eProp = GetProperty(chunk, "endDate");
                if (eProp.HasValue && eProp.Value.ValueKind == JsonValueKind.String)
                {
                    string eVal = eProp.Value.GetString();
                    if (DateTime.TryParseExact(eVal, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed))
                    {
                        aiEndDate = aiEndDate == null || ed > aiEndDate ? ed : aiEndDate;
                    }
                }

                // Merge estimatedHours
                var hProp = GetProperty(chunk, "estimatedHours");
                if (hProp.HasValue && hProp.Value.ValueKind == JsonValueKind.Number)
                {
                    estimatedHours = (estimatedHours ?? 0) + hProp.Value.GetDecimal();
                }

                // Merge requiredSkills
                var skillsProp = GetProperty(chunk, "requiredSkills");
                if (skillsProp.HasValue && skillsProp.Value.ValueKind == JsonValueKind.Array)
                {
                    mergedSkills.AddRange(skillsProp.Value.EnumerateArray());
                }
            }

            // ⭐ LAYER 3: Override timeline
            DateTime finalStartDate;
            DateTime finalEndDate;

            if (projectStartDate != null && projectEndDate != null)
            {
                if (projectStartDate > projectEndDate)
                {
                    finalStartDate = aiStartDate ?? DateTime.Now;
                    finalEndDate = aiEndDate ?? DateTime.Now.AddDays(45);
                }
                else
                {
                    finalStartDate = projectStartDate.Value;
                    finalEndDate = projectEndDate.Value;
                }
            }
            else
            {
                finalStartDate = aiStartDate ?? DateTime.Now;
                finalEndDate = aiEndDate ?? DateTime.Now.AddDays(45);
            }

            // Convert JsonElements to DTOs
            var subtasksDto = new List<GeminiSubtaskDto>();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var item in mergedSubtasks)
            {
                try {
                    var dto = JsonSerializer.Deserialize<GeminiSubtaskDto>(item.GetRawText(), options);
                    if (dto != null)
                    {
                        // ⭐ Deduplicate subtasks theo title (tránh trùng khi multi-chunk)
                        var normalizedTitle = dto.Title?.Trim() ?? "";
                        if (seenTitles.Contains(normalizedTitle))
                        {
                            Console.WriteLine($"⚠️ Bỏ subtask trùng: \"{normalizedTitle}\"");
                            continue; // Skip subtask trùng tên
                        }
                        seenTitles.Add(normalizedTitle);

                        // Deduplicate và giới hạn skill cho subtask (max 4)
                        dto.RequiredSkills = dto.RequiredSkills
                            .GroupBy(s => s.SkillName.Trim().ToLower())
                            .Select(g => g.OrderByDescending(s => s.Importance).ThenByDescending(s => s.RequiredLevel).First())
                            .Take(4)
                            .ToList();
                        subtasksDto.Add(dto);
                    }
                } catch {}
            }

            var skillsDto = new List<GeminiSkillRequirementDTO>();
            foreach (var item in mergedSkills)
            {
                try {
                    var dto = JsonSerializer.Deserialize<GeminiSkillRequirementDTO>(item.GetRawText(), options);
                    if (dto != null) skillsDto.Add(dto);
                } catch {}
            }

            // Deduplicate và giới hạn skill cho task cha (max 8)
            var finalSkills = skillsDto
                .GroupBy(s => s.SkillName.Trim().ToLower())
                .Select(g => g.OrderByDescending(s => s.Importance).ThenByDescending(s => s.RequiredLevel).First())
                .OrderByDescending(s => s.Importance)
                .ThenByDescending(s => s.RequiredLevel)
                .Take(8)
                .ToList();

            return new GeminiTaskDto
            {
                Title = title,
                Description = description,
                StartDate = finalStartDate,
                EndDate = finalEndDate,
                EstimatedHours = estimatedHours,
                RequiredSkills = finalSkills,
                Subtasks = subtasksDto
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
        // --- Helper Methods for Work Packages ---

        private List<GeminiSubtaskDto> GenerateWorkPackages(DocTask.Service.Helpers.ModuleExtractor.ModuleInfo module, List<string> techStack, DateTime start, DateTime end)
        {
            var packages = new List<GeminiSubtaskDto>();
            var totalHours = (decimal)module.Hours;
            var totalDays = (end - start).TotalDays;
            
            // Standard phases allocation
            var standardPkgs = new [] 
            {
                new { Name = "Phân tích & Thiết kế", Ratio = 0.15m },
                new { Name = "Backend Development", Ratio = 0.40m },
                new { Name = "Frontend Development", Ratio = 0.30m },
                new { Name = "Testing", Ratio = 0.10m },
                new { Name = "DevOps & Deploy", Ratio = 0.05m }
            };

            var currentPkgStart = start;
            
            foreach (var pkg in standardPkgs)
            {
                var pkgHours = totalHours * pkg.Ratio;
                var pkgDurationDays = totalDays * (double)pkg.Ratio;
                
                // Ensure min duration
                if (pkgDurationDays < 0.2) pkgDurationDays = 0.2; // Min ~2-3 hours
                
                var pkgEnd = currentPkgStart.AddDays(pkgDurationDays);
                if (pkgEnd > end) pkgEnd = end;

                // Adjust overlaps slightly if needed, but sequential is fine for now
                
                var subtask = new GeminiSubtaskDto
                {
                    Title = $"{pkg.Name}", // e.g. "Backend Development"
                    Description = $"{pkg.Name} cho module {module.Name}",
                    EstimatedHours = pkgHours,
                    StartDate = currentPkgStart,
                    DueDate = pkgEnd,
                    RequiredSkills = MapSkills(pkg.Name, techStack)
                };
                packages.Add(subtask);
                
                currentPkgStart = pkgEnd; 
            }
            
            return packages;
        }

        private List<GeminiSkillRequirementDTO> MapSkills(string pkgName, List<string> techStack)
        {
            var skills = new List<GeminiSkillRequirementDTO>();
            var stack = techStack ?? new List<string>();

            if (pkgName.Contains("Backend"))
            {
                var keys = new[] { ".NET", "C#", "Java", "Python", "Node", "Go", "PHP", "SQL", "Entity", "Dapper" };
                foreach(var t in stack) 
                    if(keys.Any(k => t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                        skills.Add(new GeminiSkillRequirementDTO { SkillName = t, RequiredLevel = 3, Importance = 5 });
                
                if (!skills.Any()) skills.Add(new GeminiSkillRequirementDTO { SkillName = "C# .NET", RequiredLevel = 3, Importance = 5 });
            }
            else if (pkgName.Contains("Frontend"))
            {
                var keys = new[] { "React", "Vue", "Angular", "HTML", "CSS", "JS", "TypeScript", "Tailwind", "Bootstrap" };
                foreach(var t in stack) 
                    if(keys.Any(k => t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                        skills.Add(new GeminiSkillRequirementDTO { SkillName = t, RequiredLevel = 3, Importance = 5 });

                if (!skills.Any()) skills.Add(new GeminiSkillRequirementDTO { SkillName = "ReactJS", RequiredLevel = 3, Importance = 5 });
            }
            else if (pkgName.Contains("Testing"))
            {
                 skills.Add(new GeminiSkillRequirementDTO { SkillName = "Software Testing", RequiredLevel = 3, Importance = 5 });
                 if (stack.Any(s => s.Contains("Selenium") || s.Contains("Unit")))
                    skills.Add(new GeminiSkillRequirementDTO { SkillName = "Automation Test", RequiredLevel = 3, Importance = 4 });
            }
            else if (pkgName.Contains("Design"))
            {
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "System Design", RequiredLevel = 4, Importance = 5 });
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Database Design", RequiredLevel = 4, Importance = 5 });
            }
            else if (pkgName.Contains("DevOps"))
            {
                var keys = new[] { "Docker", "Kubernetes", "AWS", "Azure", "CI/CD", "Jenkins", "Git" };
                foreach(var t in stack) 
                    if(keys.Any(k => t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                        skills.Add(new GeminiSkillRequirementDTO { SkillName = t, RequiredLevel = 3, Importance = 5 });
                
                if (!skills.Any()) skills.Add(new GeminiSkillRequirementDTO { SkillName = "DevOps", RequiredLevel = 3, Importance = 5 });
            }

            return skills.DistinctBy(s => s.SkillName).ToList();
        }
    }
}
