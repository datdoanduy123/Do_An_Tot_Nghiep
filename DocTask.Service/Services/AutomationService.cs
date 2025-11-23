using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Gemini;
using DocTask.Core.Dtos.SubTasks;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Service.Helpers;

namespace DocTask.Service.Services
{
    public class AutomationService : IAutomationService
    {
        private readonly HttpClient _httpClient;
        private readonly string _geminiApiKey;
        private readonly IUploadFileRepository _uploadFileRepository;
        private readonly ITaskService _taskService;
        private readonly ISubTaskService _subTaskService;
        private readonly IFileConvertService _fileConvertService;
        private const int FileContextChunkSize = 3000;
        private const int FileContentMaxLength = 6000;
        private const int FilePreviewLength = 500;
        private static readonly JsonSerializerOptions ActionDeserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };
        public AutomationService(
            HttpClient httpClient,
            GeminiDto.GeminiOptions options,
            IUploadFileRepository uploadFileRepository,
            IUploadFileService uploadFileService,
            ITaskRepository taskRepository,
            ITaskService taskService,
            ISubTaskService subTaskService,
            IProgressRepository progressRepository,
            IFileConvertService fileConvertService,
            IAgentRepository agentRepository
        )
        {
            _httpClient = httpClient;
            _geminiApiKey = options.ApiKey;
            _uploadFileRepository = uploadFileRepository;
            _taskService = taskService;
            _subTaskService = subTaskService;
            _fileConvertService = fileConvertService;
        }
        
        public async Task<TaskAutomationDto> TaskAnalyzeAsync(AgentRequestDto request, bool redo = false)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var (aggregatedContext, contextFiles, missingFileIds) = await BuildFileContextAsync(request.FileIds ?? Enumerable.Empty<int>());
            var systemPrompt = $"{AgentPrompt.TaskAutomationAgent}\nCurrent UTC time: {DateTime.UtcNow:O}";
            var trimmedPrompt = string.IsNullOrWhiteSpace(request.Prompt)
                ? "No explicit user prompt provided. Derive actions from document context only."
                : request.Prompt.Trim();

            var requestBody = new
            {
                contents = new List<object>
                {
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new
                            {
                                text = PromptHelper.Clean(systemPrompt)
                            }
                        }
                    },
                    new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new
                            {
                                text = PromptHelper.Clean(trimmedPrompt)
                            }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0,
                    candidateCount = 1,
                    topP = 0.8,
                    topK = 40,
                    maxOutputTokens = 4096,
                }
            };

            if (!string.IsNullOrWhiteSpace(aggregatedContext))
            {
                var chunks = SplitIntoChunks(aggregatedContext, FileContextChunkSize);
                for (int i = 0; i < chunks.Count; i++)
                {
                    requestBody.contents.Add(new
                    {
                        role = "user",
                        parts = new object[]
                        {
                            new
                            {
                                text = PromptHelper.Clean($"Document context chunk {i + 1}/{chunks.Count}:\n{chunks[i]}")
                            }
                        }
                    });
                }
            }
            else
            {
                requestBody.contents.Add(new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new
                        {
                            text = PromptHelper.Clean("No document context supplied.")
                        }
                    }
                });
            }

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={_geminiApiKey}";

            var httpResponse = await _httpClient.PostAsJsonAsync(url, requestBody);
            var raw = await httpResponse.Content.ReadAsStringAsync();
            httpResponse.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(raw);
            var response = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            var candidateJson = ExtractJsonArray(response);
            if (string.IsNullOrWhiteSpace(candidateJson) || candidateJson.TrimStart()[0] != '[' || candidateJson.TrimEnd()[^1] != ']')
            {
                throw new InvalidOperationException("AI output is not a valid JSON array.");
            }
            
            Console.WriteLine("Raw Response:\n" + response);
            List<CreateTaskAutomationDto>? tasks;
            try
            {
                tasks = JsonSerializer.Deserialize<List<CreateTaskAutomationDto>>(candidateJson, ActionDeserializeOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("AI response is not a valid JSON action list.", ex);
            }

            tasks ??= new List<CreateTaskAutomationDto>();
            NormalizeTasks(tasks);

            return new TaskAutomationDto
            {
                Tasks = tasks,
            };
        }

        public async Task<List<TaskExecutionResultDto>> ExecuteActionAsync(TaskAutomationDto tasks, int userId)
        {
            // assignedUserIds && assignedUnitIds
            if (tasks == null)
            {
                throw new ArgumentNullException(nameof(tasks));
            }

            var results = new List<TaskExecutionResultDto>();
            NormalizeTasks(tasks.Tasks);

            foreach (var task in tasks.Tasks)
            {
                var result = new TaskExecutionResultDto
                {
                    TaskRequest = task,
                    CreatedSubTask = new List<SubtaskExecutionResultDto>()
                };

                try
                {
                    var createTaskDto = BuildCreateTaskDto(task);
                    var createdTask = await _taskService.CreateTaskAsync(createTaskDto, userId);
                    result.CreatedTask = createdTask;

                    if (task.Subtasks?.Count > 0)
                    {
                        foreach (var sub in task.Subtasks)
                        {
                            var subResult = new SubtaskExecutionResultDto { };

                            try
                            {
                                var createSubtaskDto = BuildCreateSubtaskDto(sub);
                                var createdSubtask = await _subTaskService.CreateAsync(createdTask.TaskId, createSubtaskDto, userId);
                                subResult.SubTask = createdSubtask;
                                subResult.Success = true;
                                subResult.Message = "Subtask created succesfully.";
                            }
                            catch (Exception ex)
                            {
                                subResult.Success = false;
                                subResult.Message = ex.Message;
                            }

                            result.CreatedSubTask.Add(subResult);
                        }
                    }

                    result.Success = true;
                    result.Message = "Task created succesfully.";
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.Message = ex.Message;
                }

                results.Add(result);
            }

            return results;
        }

        private async Task<(string AggregatedContext, List<FileContextDto> ContextFiles, List<int> MissingFileIds)> BuildFileContextAsync(IEnumerable<int> fileIds)
        {
            var aggregated = new StringBuilder();
            var contextFiles = new List<FileContextDto>();
            var missing = new List<int>();

            if (fileIds == null)
            {
                return (string.Empty, contextFiles, missing);
            }

            foreach (var fileId in fileIds.Distinct())
            {
                string fileName = string.Empty;
                try
                {
                    var file = await _uploadFileRepository.GetByIdAsync(fileId);
                    if (file == null)
                    {
                        missing.Add(fileId);
                        continue;
                    }

                    fileName = file.FileName;
                    var content = await _fileConvertService.GetFileContentAsync(file.FilePath);
                    var truncatedContent = TrimContent(content, FileContentMaxLength);
                    aggregated.AppendLine($"[FileId: {fileId} | FileName: {file.FileName}]");
                    aggregated.AppendLine(truncatedContent);
                    aggregated.AppendLine();
                    var preview = TrimContent(content, FilePreviewLength, appendEllipsis: true);

                    contextFiles.Add(new FileContextDto
                    {
                        FileId = fileId,
                        FileName = file.FileName,
                        Preview = preview
                    });
                }
                catch (Exception ex)
                {
                    missing.Add(fileId);
                    contextFiles.Add(new FileContextDto
                    {
                        FileId = fileId,
                        FileName = string.IsNullOrEmpty(fileName) ? $"#{fileId}" : fileName,
                        Preview = $"[error] {ex.Message}"
                    });
                }
            }

            return (aggregated.ToString(), contextFiles, missing);
        }
        
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

        private static string TrimContent(string? content, int limit, bool appendEllipsis = false)
        {
            if (string.IsNullOrEmpty(content) || limit <= 0)
            {
                return content ?? string.Empty;
            }

            if (content.Length <= limit)
            {
                return content;
            }

            var trimmed = content.Substring(0, limit);
            return appendEllipsis ? $"{trimmed}..." : trimmed;
        }

        private static string ExtractJsonArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "[]";
            }

            var trimmed = text.Trim();

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                return trimmed;
            }

            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                return $"[{trimmed}]";
            }

            var match = Regex.Match(text, "\\[[\\s\\S]*\\]");
            if (match.Success)
            {
                return match.Value;
            }

            throw new InvalidOperationException("AI output does not contain a JSON array or object.");
        }

        private static void NormalizeTasks(List<CreateTaskAutomationDto> tasks)
        {
            foreach (var task in tasks)
            {
                task.Title = task.Title?.Trim() ?? string.Empty;
                task.Description = task.Description?.Trim() ?? string.Empty;

                DateTime? taskStartDate = ParseDate(task.StartDate, nameof(task.StartDate));
                DateTime? taskDueDate = ParseDate(task.DueDate, nameof(task.DueDate));
                ApplyDateDefaults(ref taskStartDate, ref taskDueDate);
                task.StartDate = taskStartDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                task.DueDate = taskDueDate?.ToString("yyyy-MM-dd") ?? string.Empty;

                task.Subtasks ??= new();
                foreach (var sub in task.Subtasks)
                {
                    sub.Title = sub.Title?.Trim() ?? string.Empty;
                    sub.Description = sub.Description?.Trim() ?? string.Empty;

                    DateTime? subStartDate = ParseDate(sub.StartDate, nameof(sub.StartDate));
                    DateTime? subDueDate = ParseDate(sub.DueDate, nameof(sub.DueDate));
                    ApplyDateDefaults(ref subStartDate, ref subDueDate);
                    sub.StartDate = subStartDate?.ToString("yyyy-MM-dd") ?? string.Empty;
                    sub.DueDate = subDueDate?.ToString("yyyy-MM-dd") ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(sub.Frequency) || !IsValidFrequency(sub.Frequency))
                    {
                        sub.Frequency = "daily";
                    }
                    else
                    {
                        sub.Frequency = sub.Frequency.Trim().ToLower();
                    }

                    if (sub.IntervalValue <= 0)
                    {
                        sub.IntervalValue = 1;
                    }
                    
                    sub.Days = sub.Days?.Where(d => d >= 0).ToList() ?? new List<int>();
                    sub.AssignedUserIds ??= new();
                    sub.AssignedUnitIds ??= new();
                    NormalizeDays(sub.Frequency, sub.Days);
                }
            }
        }

        private static void NormalizeDays(string frequency, List<int> days)
        {
            if (days == null)
            {
                days.Add(0);
            }

            if (frequency == "daily")
            {
                days.Clear();
                days.Add(0);
            }
            else if (frequency == "weekly")
            {
                days.RemoveAll(d => d < 1 || d > 7);
            }
            else if (frequency == "monthly")
            {
                days.RemoveAll(d => d < 1 || d > 30);
            }
        }
        
        private static bool IsValidFrequency(string value)
        {
            return
                value.Equals("daily", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("weekly", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("monthly", StringComparison.OrdinalIgnoreCase);
        }

        private static CreateTaskDto BuildCreateTaskDto(CreateTaskAutomationDto task)
        {
            if (task == null)
            {
                throw new InvalidOperationException("Task is null");
            }

            DateTime? startDate = string.IsNullOrWhiteSpace(task.StartDate)
                ? null
                : ParseDate(task.StartDate, nameof(task.StartDate));
            DateTime? dueDate = string.IsNullOrWhiteSpace(task.DueDate)
                ? null
                : ParseDate(task.DueDate, nameof(task.DueDate));
            ApplyDateDefaults(ref startDate, ref dueDate);

            if (!startDate.HasValue || !dueDate.HasValue)
            {
                throw new InvalidOperationException("StartDate/DueDate missing after defaults.");
            }

            return new CreateTaskDto
            {
                Title = task.Title,
                Description = task.Description,
                StartDate = startDate.Value,
                DueDate = dueDate.Value,
                Frequency = task.Frequency ?? string.Empty,
                IntervalValue = task.IntervalValue,
                Days = task.Days ?? new List<int>(),
            };
        }
        
        private static CreateSubTaskRequest BuildCreateSubtaskDto(CreateSubTaskAutomationDto subtask)
        {
            if (subtask == null)
            {
                throw new InvalidOperationException("Payload subtask is null");
            }

            DateTime? startDate = string.IsNullOrWhiteSpace(subtask.StartDate)
                ? null
                : ParseDate(subtask.StartDate, nameof(subtask.StartDate));
            DateTime? dueDate = string.IsNullOrWhiteSpace(subtask.DueDate)
                ? null
                : ParseDate(subtask.DueDate, nameof(subtask.DueDate));
            ApplyDateDefaults(ref startDate, ref dueDate);

            if (!startDate.HasValue || !dueDate.HasValue)
            {
                throw new InvalidOperationException("StartDate/DueDate missing after defaults.");
            }

            return new CreateSubTaskRequest
            {
                Title = subtask.Title,
                Description = subtask.Description,
                StartDate = startDate.Value,
                DueDate = dueDate.Value,
                Frequency = subtask.Frequency ?? string.Empty,
                IntervalValue = subtask.IntervalValue,
                Days = subtask.Days ?? new List<int>(),
            };
        }

        private static DateTime? ParseDate(string? raw, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            {
                return dto.UtcDateTime;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dtLocal))
            {
                return DateTime.SpecifyKind(dtLocal, DateTimeKind.Local).ToUniversalTime();
            }

            throw new InvalidOperationException($"Invalid date for '{fieldName}': {raw}");
        }

        private static void ApplyDateDefaults(ref DateTime? start, ref DateTime? due)
        {
            var todayUtc = DateTime.UtcNow.Date;

            if (!start.HasValue && !due.HasValue)
            {
                start = todayUtc;
                due = start.Value.AddDays(7);
            }
            else if (!start.HasValue && due.HasValue)
            {
                start = due.Value;
            }
            else if (start.HasValue && !due.HasValue)
            {
                due = start.Value;
            }

            if (start.HasValue && due.HasValue && due.Value < start.Value)
            {
                due = start.Value.AddDays(3);
            }
        }
    }
}