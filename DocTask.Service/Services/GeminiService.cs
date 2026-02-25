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
            Console.WriteLine($"✅ [GeminiService] Detected {projectInfo.Modules.Count} modules entirely via Template → map to Epics.");

            var pStart = projectStartDate ?? DateTime.Now;
            var pEnd   = projectEndDate ?? DateTime.Now.AddDays(30);

            // ⭐ Map Module → GeminiEpicDto (Level 2)
            var totalHours = (double)projectInfo.Modules.Sum(m => m.Hours);
            if (totalHours <= 0) totalHours = 1;
            var totalDays  = (pEnd - pStart).TotalDays;
            var epicStart  = pStart;
            var epicsList  = new List<GeminiEpicDto>();

            foreach (var m in projectInfo.Modules)
            {
                var ratio       = m.Hours / totalHours;
                var durationDays = Math.Max(1.0, totalDays * ratio);
                var epicEnd     = epicStart.AddDays(durationDays);
                if (epicEnd > pEnd) epicEnd = pEnd;

                // ⭐ Sinh Work Packages dưới dạng GeminiSubtaskDto (Level 3)
                var tasks = GenerateWorkPackages(m, projectInfo.TechStack, epicStart, epicEnd);

                // ⭐ A: Aggregate Epic skills từ child tasks (distinct, top 5 quan trọng nhất)
                var epicSkills = tasks
                    .SelectMany(t => t.RequiredSkills ?? new List<GeminiSkillRequirementDTO>())
                    .GroupBy(s => s.SkillName)
                    .Select(g => g.OrderByDescending(s => s.Importance).First())
                    .OrderByDescending(s => s.Importance)
                    .Take(5)
                    .ToList();

                epicsList.Add(new GeminiEpicDto
                {
                    Title          = $"Epic: {m.Name}",
                    Description    = m.Desc,
                    EstimatedHours = m.Hours,
                    StartDate      = epicStart,
                    DueDate        = epicEnd,
                    // ⭐ Dùng epic skills đã aggregate thay vì list rỗng
                    RequiredSkills = epicSkills,
                    Tasks          = tasks
                });

                epicStart = epicEnd;
            }

            // ⭐ B: Root task skills = top 5 skills phổ biến nhất trên toàn project
            // Gom tất cả skills từ mọi epic, đếm tần suất → lấy top 5
            var rootSkills = epicsList
                .SelectMany(e => e.RequiredSkills ?? new List<GeminiSkillRequirementDTO>())
                .GroupBy(s => s.SkillName)
                .Select(g => new GeminiSkillRequirementDTO
                {
                    SkillName      = g.Key,
                    RequiredLevel  = (int)g.Average(s => s.RequiredLevel),
                    Importance     = g.Max(s => s.Importance) // lấy importance cao nhất
                })
                .OrderByDescending(s => s.Importance)
                .Take(5)
                .ToList();

            var aiTask = new GeminiTaskDto
            {
                Title          = !string.IsNullOrWhiteSpace(projectInfo.Title) ? projectInfo.Title : "Dự án mới",
                Description    = !string.IsNullOrWhiteSpace(projectInfo.Description) ? projectInfo.Description : "Được trích xuất từ template tài liệu.",
                StartDate      = pStart,
                EndDate        = pEnd,
                EstimatedHours = (decimal)totalHours,
                // ⭐ Root task có skills tổng hợp từ toàn bộ project
                RequiredSkills = rootSkills,
                Epics          = epicsList
            };

            // TaskDraftService.CreateDraftFromGeminiAsync sẽ tự xử lý Epics
            var createdDraftId = await _taskDraftService.CreateDraftFromGeminiAsync(aiTask, fileId, userId);

            return new ChatResponse
            {
                Response  = $"Đã trích xuất thành công dự án \"{aiTask.Title}\" với {aiTask.Epics.Count} epic từ tài liệu.",
                DraftId   = createdDraftId,
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

        // Lưu vào Draft — TaskDraftService sẽ tự đọc Epics và lưu 3 level
        var draftId = await _taskDraftService.CreateDraftFromGeminiAsync(merged, fileId, userId);

        var response = new ChatResponse
        {
            Response   = "Đã tạo bản nháp thành công",
            DraftId    = draftId,
            AiResponse = merged
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

        /// <summary>
        /// Merge nhiều chunk JSON từ AI thành 1 GeminiTaskDto 3 cấp.
        /// Hỗ trợ cả schema cũ (subtasks) và schema mới (epics[].tasks[]).
        /// </summary>
        private GeminiTaskDto MergeChunkResponses(
            List<JsonElement> chunkResponses,
            DateTime? projectStartDate = null,
            DateTime? projectEndDate   = null
        )
        {
            if (chunkResponses.Count == 0) return new GeminiTaskDto();

            var firstChunk = chunkResponses[0];
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            string[] dateFormats = { "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy", "yyyy/MM/dd" };

            // ── Helper: lấy property case-insensitive ────────────────────
            JsonElement? GetProp(JsonElement el, string name)
            {
                if (el.TryGetProperty(name, out var p)) return p;
                if (el.TryGetProperty(char.ToUpper(name[0]) + name.Substring(1), out var pU)) return pU;
                return null;
            }

            // ── Lấy metadata từ chunk đầu tiên ──────────────────────────
            var title       = GetProp(firstChunk, "title")?.GetString() ?? "Untitled Project";
            var description = GetProp(firstChunk, "description")?.GetString() ?? "";

            var mergedEpics  = new List<JsonElement>();
            var mergedSkills = new List<JsonElement>();
            DateTime? aiStart = null;
            DateTime? aiEnd   = null;
            decimal?  totalHours = null;

            foreach (var chunk in chunkResponses)
            {
                // ── Merge epics[] (schema mới 3 level) ─────────────────
                var epicsProp = GetProp(chunk, "epics");
                if (epicsProp.HasValue && epicsProp.Value.ValueKind == JsonValueKind.Array)
                    mergedEpics.AddRange(epicsProp.Value.EnumerateArray());

                // ── Fallback: merge subtasks[] (schema cũ 2 level) ─────
                if (!mergedEpics.Any())
                {
                    var subtasksProp = GetProp(chunk, "subtasks");
                    if (subtasksProp.HasValue && subtasksProp.Value.ValueKind == JsonValueKind.Array)
                        mergedEpics.AddRange(subtasksProp.Value.EnumerateArray());
                }

                // ── Project timeline ───────────────────────────────────
                var sRaw = GetProp(chunk, "startDate")?.GetString();
                if (sRaw != null && DateTime.TryParseExact(sRaw, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var sd))
                    aiStart = aiStart == null || sd < aiStart ? sd : aiStart;

                var eRaw = GetProp(chunk, "endDate")?.GetString();
                if (eRaw != null && DateTime.TryParseExact(eRaw, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ed))
                    aiEnd = aiEnd == null || ed > aiEnd ? ed : aiEnd;

                var hProp = GetProp(chunk, "estimatedHours");
                if (hProp.HasValue && hProp.Value.ValueKind == JsonValueKind.Number)
                    totalHours = (totalHours ?? 0) + hProp.Value.GetDecimal();

                // ── Merge project-level skills ─────────────────────────
                var skillsProp = GetProp(chunk, "requiredSkills");
                if (skillsProp.HasValue && skillsProp.Value.ValueKind == JsonValueKind.Array)
                    mergedSkills.AddRange(skillsProp.Value.EnumerateArray());
            }

            // ── Override timeline với giá trị extract được từ file ─────
            DateTime finalStart, finalEnd;
            if (projectStartDate.HasValue && projectEndDate.HasValue && projectStartDate <= projectEndDate)
            { finalStart = projectStartDate.Value; finalEnd = projectEndDate.Value; }
            else
            { finalStart = aiStart ?? DateTime.Now; finalEnd = aiEnd ?? DateTime.Now.AddDays(45); }

            // ── Deserialize Epics (dedup theo title) ───────────────────
            var seenEpics  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var epicsDto   = new List<GeminiEpicDto>();

            foreach (var item in mergedEpics)
            {
                try
                {
                    var dto = JsonSerializer.Deserialize<GeminiEpicDto>(item.GetRawText(), jsonOptions);
                    if (dto == null) continue;
                    var key = dto.Title?.Trim() ?? "";
                    if (seenEpics.Contains(key)) continue;
                    seenEpics.Add(key);

                    // Dedup + giới hạn skill epic (max 5)
                    dto.RequiredSkills = dto.RequiredSkills
                        .GroupBy(s => s.SkillName.Trim().ToLower())
                        .Select(g => g.OrderByDescending(s => s.Importance).ThenByDescending(s => s.RequiredLevel).First())
                        .Take(5).ToList();

                    // Dedup task bên trong epic (max 4 skill/task)
                    var seenTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var cleanedTasks = new List<GeminiSubtaskDto>();
                    foreach (var t in dto.Tasks)
                    {
                        var tKey = t.Title?.Trim() ?? "";
                        if (seenTasks.Contains(tKey)) continue;
                        seenTasks.Add(tKey);
                        t.RequiredSkills = t.RequiredSkills
                            .GroupBy(s => s.SkillName.Trim().ToLower())
                            .Select(g => g.OrderByDescending(s => s.Importance).ThenByDescending(s => s.RequiredLevel).First())
                            .Take(4).ToList();
                        cleanedTasks.Add(t);
                    }
                    dto.Tasks = cleanedTasks;
                    epicsDto.Add(dto);
                }
                catch { /* bỏ qua epic lỗi parse */ }
            }

            // ── Dedup + giới hạn skill project (max 8) ─────────────────
            var skillsDto = mergedSkills
                .Select(item => { try { return JsonSerializer.Deserialize<GeminiSkillRequirementDTO>(item.GetRawText(), jsonOptions); } catch { return null; } })
                .Where(x => x != null)
                .GroupBy(s => s!.SkillName.Trim().ToLower())
                .Select(g => g.OrderByDescending(s => s!.Importance).ThenByDescending(s => s!.RequiredLevel).First())
                .OrderByDescending(s => s!.Importance).ThenByDescending(s => s!.RequiredLevel)
                .Take(8)
                .Cast<GeminiSkillRequirementDTO>()
                .ToList();

            return new GeminiTaskDto
            {
                Title          = title,
                Description    = description,
                StartDate      = finalStart,
                EndDate        = finalEnd,
                EstimatedHours = totalHours,
                RequiredSkills = skillsDto,
                Epics          = epicsDto
            };
        }

        /// <summary>
        /// Bridge: chuyển đổi GeminiTaskDto (có Epics[]) sang dạng GeminiTaskDto
        /// với Epics map thành GeminiSubtaskDto có Subtasks — để TaskDraftService
        /// tái sử dụng logic đệ quy CreateSubTaskDrafts() mà không cần sửa.
        /// Level 1: Project (root)
        /// Level 2: Epic → SubtaskDto cha (ParentDraftId = root)
        /// Level 3: Task → SubtaskDto con (ParentDraftId = epic)
        /// </summary>
        private GeminiTaskDto BridgeEpicsToSubtasks(GeminiTaskDto source)
        {
            // Map Epic → GeminiSubtaskDto (Level 2)
            var epicAsSubtasks = source.Epics.Select(epic => new GeminiSubtaskDto
            {
                Title          = epic.Title,
                Description    = epic.Description,
                StartDate      = epic.StartDate,
                DueDate        = epic.DueDate,
                EstimatedHours = epic.EstimatedHours,
                Priority       = "Medium",
                RequiredSkills = epic.RequiredSkills,
                // Map Task (Level 3) → Subtasks lồng bên trong Epic
                Subtasks = epic.Tasks.Select(t => new GeminiSubtaskDto
                {
                    Title          = t.Title,
                    Description    = t.Description,
                    StartDate      = t.StartDate,
                    DueDate        = t.DueDate,
                    EstimatedHours = t.EstimatedHours,
                    Priority       = t.Priority,
                    RequiredSkills = t.RequiredSkills,
                    Subtasks       = new List<GeminiSubtaskDto>() // Level 3 không có con
                }).ToList()
            }).ToList();

            // ⭐ BridgeEpicsToSubtasks kết thúc tại đây.
            // epicAsSubtasks = danh sách Epic đã map thành SubtaskDto (có Subtasks lồng = Tasks)
            // Cần dùng một object wrapper để truyền sang TaskDraftService
            // Vì GeminiTaskDto đã bỏ Subtasks property, chúng ta sẽ truyền
            // trực tiếp 'epicAsSubtasks' vào phương thức overload của TaskDraftService
            // bằng cách tạo một GeminiTaskDto tạm với Epics set = source.Epics
            // và dùng epicAsSubtasks làm tham số riêng (xem AskWithFileAsync)
            return new GeminiTaskDto
            {
                Title          = source.Title,
                Description    = source.Description,
                StartDate      = source.StartDate,
                EndDate        = source.EndDate,
                EstimatedHours = source.EstimatedHours,
                RequiredSkills = source.RequiredSkills,
                Epics          = source.Epics, // giữ để response trả đúng
                // ⚠️ Subtasks đã bị thay bởi Epics trong DTO mới
                // TaskDraftService sẽ đọc Epics trực tiếp (xem fix bên dưới)
            };
        }

        /// <summary>
        /// Trả về epicAsSubtasks list để CreateDraftFromGeminiAsync dùng.
        /// Tách riêng vì GeminiTaskDto không còn Subtasks property.
        /// </summary>
        private List<GeminiSubtaskDto> BuildEpicSubtaskList(GeminiTaskDto source)
        {
            return source.Epics.Select(epic => new GeminiSubtaskDto
            {
                Title          = epic.Title,
                Description    = epic.Description,
                StartDate      = epic.StartDate,
                DueDate        = epic.DueDate,
                EstimatedHours = epic.EstimatedHours,
                Priority       = "Medium",
                RequiredSkills = epic.RequiredSkills,
                Subtasks       = epic.Tasks.Select(t => new GeminiSubtaskDto
                {
                    Title          = t.Title,
                    Description    = t.Description,
                    StartDate      = t.StartDate,
                    DueDate        = t.DueDate,
                    EstimatedHours = t.EstimatedHours,
                    Priority       = t.Priority,
                    RequiredSkills = t.RequiredSkills,
                    Subtasks       = new List<GeminiSubtaskDto>()
                }).ToList()
            }).ToList();
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
            var packages    = new List<GeminiSubtaskDto>();
            var totalHours  = (decimal)module.Hours;
            var totalDays   = (end - start).TotalDays;

            // ⭐ ƯU TIÊN: nếu module có features thực tế (1.1, 1.2... từ tài liệu) thì dùng chúng
            if (module.Features != null && module.Features.Count > 0)
            {
                Console.WriteLine($"  📋 [GenerateWorkPackages] Module '{module.Name}' có {module.Features.Count} features gốc → dùng làm tasks thực tế.");

                var hoursPerTask = totalHours > 0
                    ? Math.Round(totalHours / module.Features.Count, 1)
                    : 8m;
                var daysPerTask  = totalDays / module.Features.Count;
                var taskStart    = start;

                for (int idx = 0; idx < module.Features.Count; idx++)
                {
                    var feature = module.Features[idx];
                    var taskEnd = (idx == module.Features.Count - 1)
                        ? end  // task cuối kết thúc đúng epicEnd
                        : taskStart.AddDays(Math.Max(0.5, daysPerTask));
                    if (taskEnd > end) taskEnd = end;

                    // Tự động map skill từ từ khóa trong tên feature
                    var skillHint = InferSkillHintFromFeature(feature);

                    packages.Add(new GeminiSubtaskDto
                    {
                        Title          = feature,
                        Description    = $"{feature} - thuộc module {module.Name}",
                        EstimatedHours = hoursPerTask,
                        StartDate      = taskStart,
                        DueDate        = taskEnd,
                        Priority       = "Medium",
                        // ⭐ Truyền feature text để MapSkills bổ sung skills đặc thù
                        RequiredSkills = MapSkills(skillHint, techStack, feature)
                    });

                    taskStart = taskEnd;
                }

                return packages;
            }

            // ⭐ Fallback: Overlap scheduling model (thực tế hơn sequential)
            // Design → BE bắt đầu sớm → FE delay 35% → Testing sau 80% → DevOps song song Testing
            Console.WriteLine($"  ⚠️ [GenerateWorkPackages] Module '{module.Name}' không có features → dùng Overlap model.");

            // Tỷ lệ offset bắt đầu (so với tổng duration của epic)
            // và offset kết thúc — mô phỏng Finish-to-Start + Start-to-Start với lag thực tế
            var overlapPhases = new[]
            {
                // Name,                    HoursRatio, StartOffset, EndOffset  (% của totalDays)
                (Name: "Phân tích & Thiết kế", Hours: 0.12m, Start: 0.00, End: 0.20),
                (Name: "Backend Development",  Hours: 0.38m, Start: 0.10, End: 0.65), // bắt đầu khi Design 50%
                (Name: "Frontend Development", Hours: 0.28m, Start: 0.35, End: 0.85), // bắt đầu khi Backend 50%
                (Name: "Testing & QA",         Hours: 0.14m, Start: 0.70, End: 0.95), // bắt đầu khi BE+FE ~80%
                (Name: "DevOps & Deploy",      Hours: 0.08m, Start: 0.88, End: 1.00), // song song cuối Testing
            };

            foreach (var phase in overlapPhases)
            {
                // Tính ngày bắt đầu/kết thúc dựa trên offset % của tổng epic duration
                var phaseStart = start.AddDays(totalDays * phase.Start);
                var phaseEnd   = start.AddDays(totalDays * phase.End);

                // Clamp: không vượt ra ngoài epic boundary
                if (phaseStart < start) phaseStart = start;
                if (phaseEnd   > end)   phaseEnd   = end;
                if (phaseEnd   <= phaseStart) phaseEnd = phaseStart.AddDays(1);

                packages.Add(new GeminiSubtaskDto
                {
                    Title          = phase.Name,
                    Description    = $"{phase.Name} cho module {module.Name}",
                    EstimatedHours = Math.Round(totalHours * phase.Hours, 1),
                    StartDate      = phaseStart,
                    DueDate        = phaseEnd,
                    Priority       = phase.Name.Contains("Backend") ? "High" : "Medium",
                    RequiredSkills = MapSkills(phase.Name, techStack, phase.Name)
                });
            }

            return packages;
        }

        /// <summary>
        /// Suy luận loại skill cần thiết dựa trên từ khóa trong tên feature.
        /// Thứ tự ưu tiên: Testing → DevOps → Frontend → Backend (tránh trùng từ khóa)
        /// </summary>
        private string InferSkillHintFromFeature(string featureName)
        {
            var f = featureName.ToLower();

            // ⭐ 1. Testing — kiểm tra TRƯỚC để tránh bị Frontend/Backend lấy mất
            // VD: "UI Testing", "Integration Testing", "Performance Testing"
            if (f.Contains("testing") || f.Contains("test") || f.Contains(" qa") ||
                f.Contains("selenium") || f.Contains("cypress") || f.Contains("jmeter") ||
                f.Contains("performance test") || f.Contains("k6") ||
                f.Contains("owasp") || f.Contains("coverage") || f.Contains("kiểm thử") ||
                f.Contains("unit test") || f.Contains("integration test"))
                return "Testing";

            // ⭐ 2. DevOps — kiểm tra trước Frontend vì có "monitoring", "azure"
            if (f.Contains("deploy") || f.Contains("ci/cd") || f.Contains("docker") ||
                f.Contains("monitoring") || f.Contains("azure") || f.Contains("cloud") ||
                f.Contains("triển khai") || f.Contains("github actions") ||
                f.Contains("kubernetes") || f.Contains("pipeline") || f.Contains("container"))
                return "DevOps";

            // ⭐ 3. Frontend — giao diện, dashboard, báo cáo trực quan, cài đặt hệ thống
            if (f.Contains("giao diện") || f.Contains("frontend") ||
                f.Contains("dashboard") || f.Contains("form") || f.Contains("màn hình") ||
                f.Contains("responsive") || f.Contains("component") || f.Contains("page") ||
                f.Contains("trang quản") || f.Contains("hiển thị") || f.Contains("biểu đồ") ||
                f.Contains("seo") || f.Contains("thống kê") || f.Contains("báo cáo") ||
                f.Contains("cài đặt") || f.Contains("hồ sơ cá nhân") || f.Contains("banner") ||
                f.Contains("bán chạy") || f.Contains("nổi bật"))
                return "Frontend";

            // ⭐ 4. Backend — mặc định cho hầu hết business logic
            if (f.Contains("api") || f.Contains("database") || f.Contains("crud") ||
                f.Contains("backend") || f.Contains("tích hợp") || f.Contains("jwt") ||
                f.Contains("oauth") || f.Contains("otp") || f.Contains("token") ||
                f.Contains("import") || f.Contains("export") || f.Contains("webhook") ||
                f.Contains("email") || f.Contains("sms") || f.Contains("signalr"))
                return "Backend";

            // Mặc định Backend nếu không match bất kỳ từ khóa nào
            return "Backend";
        }


        /// <summary>
        /// Map skills dựa trên category (pkgName) + từ khóa đặc thù trong featureName.
        /// VD: "Tích hợp VNPay" → Backend skills + Payment Integration
        ///     "Upload file lên Azure" → Backend skills + Azure Blob Storage
        /// </summary>
        private List<GeminiSkillRequirementDTO> MapSkills(string pkgName, List<string> techStack, string featureName = "")
        {
            var skills = new List<GeminiSkillRequirementDTO>();
            var stack = techStack ?? new List<string>();
            var f = featureName.ToLower();

            // ⭐ BƯỚC 1: Thêm skills cơ bản theo category
            if (pkgName.Contains("Backend"))
            {
                var keys = new[] { ".NET", "ASP", "C#", "Java", "Python", "Node", "Go", "PHP", "SQL", "Entity", "Dapper" };
                foreach (var t in stack)
                    if (keys.Any(k => t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                        skills.Add(new GeminiSkillRequirementDTO { SkillName = t, RequiredLevel = 3, Importance = 5 });

                if (!skills.Any())
                    skills.Add(new GeminiSkillRequirementDTO { SkillName = "C# .NET", RequiredLevel = 3, Importance = 5 });
            }
            else if (pkgName.Contains("Frontend"))
            {
                var keys = new[] { "React", "Vue", "Angular", "HTML", "CSS", "TypeScript", "Tailwind", "Bootstrap" };
                foreach (var t in stack)
                    if (keys.Any(k => t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                        skills.Add(new GeminiSkillRequirementDTO { SkillName = t, RequiredLevel = 3, Importance = 5 });

                if (!skills.Any())
                    skills.Add(new GeminiSkillRequirementDTO { SkillName = "ReactJS", RequiredLevel = 3, Importance = 5 });
            }
            else if (pkgName.Contains("Testing"))
            {
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Software Testing", RequiredLevel = 3, Importance = 5 });
                // Thêm tool-specific testing skills
                if (f.Contains("cypress") || f.Contains("ui test") || f.Contains("selenium"))
                    skills.Add(new GeminiSkillRequirementDTO { SkillName = "Cypress / Selenium", RequiredLevel = 3, Importance = 4 });
                if (f.Contains("k6") || f.Contains("jmeter") || f.Contains("performance"))
                    skills.Add(new GeminiSkillRequirementDTO { SkillName = "Performance Testing (k6)", RequiredLevel = 3, Importance = 4 });
                if (f.Contains("owasp") || f.Contains("security test"))
                    skills.Add(new GeminiSkillRequirementDTO { SkillName = "Security Testing", RequiredLevel = 3, Importance = 4 });
                if (f.Contains("unit") || f.Contains("xunit") || f.Contains("nunit"))
                    skills.Add(new GeminiSkillRequirementDTO { SkillName = "Unit Testing (xUnit)", RequiredLevel = 3, Importance = 4 });
            }
            else if (pkgName.Contains("Design") || pkgName.Contains("Phân tích") || pkgName.Contains("Thiết kế"))
            {
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "System Design", RequiredLevel = 4, Importance = 5 });
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Database Design", RequiredLevel = 4, Importance = 5 });
            }
            else if (pkgName.Contains("DevOps"))
            {
                var keys = new[] { "Docker", "Kubernetes", "AWS", "Azure", "CI/CD", "Jenkins", "Git", "GitHub" };
                foreach (var t in stack)
                    if (keys.Any(k => t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0))
                        skills.Add(new GeminiSkillRequirementDTO { SkillName = t, RequiredLevel = 3, Importance = 5 });

                if (!skills.Any())
                    skills.Add(new GeminiSkillRequirementDTO { SkillName = "DevOps", RequiredLevel = 3, Importance = 5 });
            }

            // ⭐ BƯỚC 2: Bổ sung skills đặc thù từ nội dung feature (áp dụng cho mọi category)
            // Thanh toán
            if (f.Contains("vnpay") || f.Contains("momo") || f.Contains("thanh toán") || f.Contains("payment") || f.Contains("hóa đơn"))
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Payment Integration", RequiredLevel = 3, Importance = 5 });

            // Bảo mật / Auth
            if (f.Contains("jwt") || f.Contains("oauth") || f.Contains("2fa") || f.Contains("otp") ||
                f.Contains("bảo mật") || f.Contains("xác thực") || f.Contains("phân quyền") || f.Contains("token"))
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Security / Authentication", RequiredLevel = 3, Importance = 4 });

            // Realtime / SignalR
            if (f.Contains("signalr") || f.Contains("real-time") || f.Contains("realtime") || f.Contains("thông báo"))
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "SignalR / Realtime", RequiredLevel = 3, Importance = 4 });

            // Cloud Storage / Upload
            if (f.Contains("upload") || f.Contains("azure blob") || f.Contains("storage") || f.Contains("cloud") || f.Contains("giấy tờ"))
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Azure Blob Storage", RequiredLevel = 3, Importance = 4 });

            // Video / Streaming
            if (f.Contains("video") || f.Contains("streaming") || f.Contains("media"))
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Video Streaming", RequiredLevel = 3, Importance = 4 });

            // Email / SMS
            if (f.Contains("email") || f.Contains("sms") || f.Contains("phiếu lương") || f.Contains("thư mời"))
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Email / Notification Service", RequiredLevel = 2, Importance = 3 });

            // QR Code / Barcode
            if (f.Contains("qr") || f.Contains("barcode") || f.Contains("mã qr"))
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "QR Code Integration", RequiredLevel = 2, Importance = 3 });

            // Excel / PDF Export
            if (f.Contains("excel") || f.Contains("pdf") || f.Contains("xuất") || f.Contains("export"))
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Report / Export (Excel/PDF)", RequiredLevel = 2, Importance = 3 });

            // API tích hợp bên thứ 3
            if (f.Contains("tích hợp") || f.Contains("api") || f.Contains("ghn") || f.Contains("ghtk") || f.Contains("webhook"))
                skills.Add(new GeminiSkillRequirementDTO { SkillName = "Third-party API Integration", RequiredLevel = 3, Importance = 4 });

            return skills.DistinctBy(s => s.SkillName).Take(4).ToList(); // Giới hạn 4 skills để không quá dài
        }
    }
}
