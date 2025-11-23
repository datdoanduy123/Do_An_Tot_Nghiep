using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DocTask.Core.Dtos.OpenAIDto;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Dtos.UploadFile;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Service.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;

namespace DocTask.Service.Services
{
    public class OpenAIService : IOpenAIService
    {
        private readonly IUploadFileRepository _uploadFileRepository;
        private readonly IUploadFileService _uploadFileService;
        private readonly IFileConvertService _fileConvertService;
        private readonly OpenAI.OpenAIClient _client;
        private readonly IProgressService _progressService;
        private readonly ITaskService _taskService;
        private const int ApiMaxRetries = 3;
        private const int FileContextChunkSize = 3000;
        private const int FileContentMaxLength = 6000;
        private const int FilePreviewLength = 500;
        private static readonly JsonSerializerOptions ActionDeserializeOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public OpenAIService(
            OpenAI.OpenAIClient client,
            IUploadFileRepository uploadFileRepository,
            IUploadFileService uploadFileService,
            IFileConvertService fileConvertService,
            IProgressService progressService,
            ITaskService taskService)
        {
            _client = client;
            _uploadFileRepository = uploadFileRepository;
            _uploadFileService = uploadFileService;
            _fileConvertService = fileConvertService;
            _progressService = progressService;
            _taskService = taskService;
        }

        public async Task<OpenAIDto.ResponseDto> AskAsync(OpenAIDto.RequestDto request)
        {
            var chat = _client.GetChatClient(OpenAIDto.Model.Chat_v4o_Mini);

            var message = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(PromptHelper.Clean(@"
                    Bạn là một trợ lý AI thông minh, thân thiện và đa năng. 
                    Bạn có thể hỗ trợ nhiều chủ đề khác nhau như lập kế hoạch, giải thích kiến thức, tư vấn, hoặc trò chuyện thường ngày. 
                    Hãy trả lời chuyên nghiệp, dễ hiểu và phù hợp với ngữ cảnh."
                )),
                ChatMessage.CreateUserMessage(PromptHelper.Clean(request.Prompt)),
            };

            var answer = await ChatAsync(chat, message);
            return new OpenAIDto.ResponseDto
            {
                Response = answer,
            };
        }

        public async Task<OpenAIDto.ResponseDto> AskWithFileAsync(OpenAIDto.RequestDto request, int fileId)
        {
            var chat = _client.GetChatClient(OpenAIDto.Model.Chat_v4o_Mini);

            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null)
            {
                throw new ArgumentException("File not found");
            }

            var fileUrl = file.FilePath;
            var content = await _fileConvertService.GetFileContentAsync(fileUrl);

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(PromptHelper.Clean(@"
                    Bạn là AI có thể đọc, phân tích nội dung file và trả lời câu hỏi."
                )),
                ChatMessage.CreateUserMessage(PromptHelper.Clean($"Nội dung file:\n {content} \n\nCâu hỏi: {request.Prompt}")),
            };

            var answer = await ChatAsync(chat, messages);
            return new OpenAIDto.ResponseDto
            {
                Response = answer,
            };
        }

        public async Task<(byte[] fileContent, string fileName, string contentType)> DownloadSummaryReportAsync(
            int taskId,
            int userId,
            string format,
            DateTime? from = null,
            DateTime? to = null,
            string? status = null,
            int? assigneeId = null
        )
        {
            var chat = _client.GetChatClient(OpenAIDto.Model.Chat_v41_Mini);

            var reports = await _progressService.ReviewSubTaskProgressAsync(taskId, from, to, status, assigneeId);
            if (reports == null || !reports.Any())
            {
                throw new Exception("Không tìm thấy báo cáo nào");
            }

            // var dataContent = JsonSerializer.Serialize(reports);

            var sb = new StringBuilder();

            foreach (var report in reports)
            {
                foreach (var scheduled in report.ScheduledProgresses)
                {
                    foreach (var progress in scheduled.Progresses)
                    {
                        if (string.IsNullOrEmpty(progress.FilePath)) continue;
                        {
                            try
                            {
                                var content = await _fileConvertService.GetFileContentAsync(progress.FilePath);
                                if (string.IsNullOrWhiteSpace(content)) continue;

                                sb.AppendLine($"Report ID: {progress.ProgressId} | {progress.FileName}");
                                sb.AppendLine(content);
                                sb.AppendLine();
                            }
                            catch (ArgumentException ex)
                            {
                                sb.AppendLine($"[Error reading {progress.FileName}: {ex.Message}]");
                            }
                        }
                    }
                }
            }

            var fileSummary = sb.ToString();

            var fileMessages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(PromptHelper.Clean(OpenAIDto.Prompts.FileAssistant)),
                ChatMessage.CreateUserMessage(PromptHelper.Clean($@"
                    Dưới đây là toàn bộ nội dung báo cáo từ các file:

                    ----------------------------
                    {fileSummary}

                    Hãy tạo một báo cáo tổng hợp chuẩn theo cấu trúc."
                )),
            };

            var response = await ChatAsync(chat, fileMessages);

            // var messages = new List<ChatMessage>
            // {
            //     ChatMessage.CreateSystemMessage(OpenAIDto.Prompts.SummaryAssistant),
            //     ChatMessage.CreateUserMessage($@"
            //         Dữ liệu từ database:
            //         {dataContent}

            //         Báo cáo tổng hợp từ các file báo cáo:
            //         {fileContent}

            //         Hãy kết hợp 2 nguồn này và xuất ra báo cáo cuối cùng theo format: 23
            //         Proposal, Result, Feedback, Nội dung tổng hợp.
            //     ")
            // };

            // var answer = await ChatAsync(chat, messages);

            var extension = format.ToLower() switch
            {
                "pdf" => ".pdf",
                "doc" or "docx" or "word" => ".docx",
                "txt" or "text" => ".txt",
                "xls" or "xlsx" or "excel" => ".xlsx",
                _ => ".pdf"
            };

            var fileName = $"Bao-cao-tong-hop_{DateTime.UtcNow:yyyy-MM-dd_HHmmssZ}{extension}";
            var name = $"Bao-Cao-Tong-Hop_{DateTime.UtcNow:yyyy-MM-dd}";
            var (fileContent, contentType) = await _fileConvertService.ConvertFileFormatAsync(response, name, format);

            using var ms = new MemoryStream(fileContent);
            var file = new FormFile(ms, 0, fileContent.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            var uploadRequest = new UploadFileRequest
            {
                File = file,
                Description = $"Báo cáo tổng hợp_{DateTime.UtcNow:yyyy-MM-dd_HHmmssZ}",
            };

            var uploadResult = await _uploadFileService.UploadFileAsync(uploadRequest, userId);

            return (fileContent, fileName, contentType);
        }

        public async Task<OpenAIDto.ListActionDto> AnalyzeAutomationAsync(OpenAIDto.AgentRequestDto request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            } 

            var (aggregatedContext, contextFiles, missingFileIds) = await BuildFileContextAsync(request.FileIds ?? Enumerable.Empty<int>());
            var chat = _client.GetChatClient(OpenAIDto.Model.Chat_v41_Mini);

            var systemPrompt = $"{OpenAIDto.Prompts.TaskAutomationAgent}\nCurrent UTC time: {DateTime.UtcNow:O}";
            var trimmedPrompt = string.IsNullOrWhiteSpace(request.Prompt)
                ? "No explicit user prompt provided. Derive actions from document context only."
                : request.Prompt.Trim();

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(PromptHelper.Clean(systemPrompt)),
                ChatMessage.CreateUserMessage(PromptHelper.Clean($"User request: {trimmedPrompt}"))
            };

            if (!string.IsNullOrWhiteSpace(aggregatedContext))
            { 
                var chunks = SplitIntoChunks(aggregatedContext, FileContextChunkSize);
                for (int i = 0; i < chunks.Count; i++)
                {
                    messages.Add(ChatMessage.CreateUserMessage($"Document context chunk {i + 1}/{chunks.Count}:\n{chunks[i]}"));
                }
            }
            else
            {
                messages.Add(ChatMessage.CreateUserMessage("No document context supplied."));
            }

            messages.Add(ChatMessage.CreateUserMessage("Return the JSON array of actions now."));
            var rawResponse = await ChatAsync(chat, messages);
            var candidateJson = ExtractJsonArray(rawResponse);

            List<OpenAIDto.ActionDto>? actions;
            try
            {
                actions = JsonSerializer.Deserialize<List<OpenAIDto.ActionDto>>(candidateJson, ActionDeserializeOptions);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException("AI response is not a valid JSON action list.", ex);
            }

            actions ??= new List<OpenAIDto.ActionDto>();
            foreach (var action in actions)
            {
                NormalizeAction(action);
            }

            return new OpenAIDto.ListActionDto
            {
                ListAction = actions,
                RawResponse = rawResponse,
                ContextFiles = contextFiles,
                MissingFileIds = missingFileIds
            };
        }

        public async Task<OpenAIDto.ActionExecutionResultDto> ExecuteActionAsync(OpenAIDto.ActionDto action, int userId)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            NormalizeAction(action);
            var executionResult = new OpenAIDto.ActionExecutionResultDto
            {
                Action = CloneAction(action)
            };

            try
            {
                if (!string.Equals(action.EntityType, "task", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Unsupported entity type '{action.EntityType}'.");
                }

                switch (action.Action.ToLowerInvariant())
                {
                    case "create":
                        var createDto = BuildCreateTaskDto(action.Payload);
                        var createdTask = await _taskService.CreateTaskAsync(createDto, userId);
                        executionResult.Success = createdTask != null;
                        executionResult.Message = createdTask != null
                            ? "Task created successfully."
                            : "Task creation returned no result.";
                        executionResult.Output = createdTask;
                        break;

                    case "update":
                        if (!action.TargetId.HasValue)
                        {
                            throw new InvalidOperationException("Update action requires targetId.");
                        }

                        var updateDto = BuildUpdateTaskDto(action.Payload);
                        var updatedTask = await _taskService.UpdateTaskAsync(action.TargetId.Value, updateDto, userId);
                        executionResult.Success = updatedTask != null;
                        executionResult.Message = updatedTask != null
                            ? $"Task {action.TargetId} updated."
                            : $"Task {action.TargetId} not found.";
                        executionResult.Output = updatedTask;
                        break;

                    case "delete":
                        if (!action.TargetId.HasValue)
                        {
                            throw new InvalidOperationException("Delete action requires targetId.");
                        }
                        
                        await _taskService.DeleteTaskAsync(action.TargetId.Value, userId);
                        executionResult.Success = true;
                        executionResult.Message = $"Task {action.TargetId} deleted.";
                        break;

                    default:
                        throw new InvalidOperationException($"Unsupported action '{action.Action}'.");
                }
            }
            catch (Exception ex)
            {
                executionResult.Success = false;
                executionResult.Message = ex.Message;
            }

            return executionResult;
        }

        public async Task<OpenAIDto.AgentExecutionResultDto> AutomateTaskWorkflowAsync(OpenAIDto.AgentRequestDto request, int userId)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var analysis = await AnalyzeAutomationAsync(request);
            var result = new OpenAIDto.AgentExecutionResultDto
            {
                PlannedActions = analysis.ListAction,
                ContextFiles = analysis.ContextFiles,
                MissingFileIds = analysis.MissingFileIds,
                RawModelOutput = analysis.RawResponse
            };

            if (!request.AutoExecute || !analysis.ListAction.Any())
            {
                return result;
            }

            foreach (var action in analysis.ListAction)
            {
                var execution = await ExecuteActionAsync(action, userId);
                result.ExecutedActions.Add(execution);
            }

            return result;
        }

        private async Task<string> ChatAsync(ChatClient chatClient, List<ChatMessage> messages)
        {
            for (int attempt = 0; attempt < ApiMaxRetries; attempt++)
            {
                try
                {
                    var response = await chatClient.CompleteChatAsync(messages);
                    var answer = response.Value.Content[0].Text.ToString().Trim();
                    var usage = response.Value.Usage;

                    if (!string.IsNullOrEmpty(answer))
                    {
                        Console.WriteLine($"----------------------------------------------          Attempt = {attempt + 1}          ----------------------------------------------");

                        if (usage != null)
                        {
                            Console.WriteLine(
                                $"{usage.InputTokenDetails}\n" +
                                $"Prompt tokens: {usage.InputTokenCount}\n" +
                                $"{usage.OutputTokenDetails}\n" +
                                $"Completion tokens: {usage.OutputTokenCount}\n" +
                                $"Total tokens used: {usage.TotalTokenCount}"
                            );
                        }
                        
                        return answer;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Retry {attempt}] Error: {ex.Message}");
                    await Task.Delay(500 * (attempt + 1));
                }
            }

            throw new Exception("AI không phản hồi sau nhiều lần thử.");
        }

        private async Task<(string AggregatedContext, List<OpenAIDto.FileContextDto> ContextFiles, List<int> MissingFileIds)> BuildFileContextAsync(IEnumerable<int> fileIds)
        {
            var aggregated = new StringBuilder();
            var contextFiles = new List<OpenAIDto.FileContextDto>();
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

                    contextFiles.Add(new OpenAIDto.FileContextDto
                    {
                        FileId = fileId,
                        FileName = file.FileName,
                        Preview = preview
                    });
                }
                catch (Exception ex)
                {
                    missing.Add(fileId);
                    contextFiles.Add(new OpenAIDto.FileContextDto
                    {
                        FileId = fileId,
                        FileName = string.IsNullOrEmpty(fileName) ? $"#{fileId}" : fileName,
                        Preview = $"[error] {ex.Message}"
                    });
                }
            }

            return (aggregated.ToString(), contextFiles, missing);
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

        private static string ExtractJsonArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "[]";
            }

            var match = Regex.Match(text, "\\[[\\s\\S]*\\]");
            return match.Success ? match.Value : text;
        }

        private static void NormalizeAction(OpenAIDto.ActionDto action)
        {
            action.Action = (action.Action ?? string.Empty).Trim();
            action.EntityType = (action.EntityType ?? string.Empty).Trim();
            action.Payload = NormalizePayload(action.Payload);

            if (!action.TargetId.HasValue && action.Payload.TryGetValue("targetId", out var targetValue))
            {
                if (int.TryParse(targetValue?.ToString(), out var parsed))
                {
                    action.TargetId = parsed;
                    action.Payload.Remove("targetId");
                }
            }
        }

        private static Dictionary<string, object> NormalizePayload(Dictionary<string, object> payload)
        {
            var normalized = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (payload == null)
            {
                return normalized;
            }

            foreach (var kvp in payload)
            {
                normalized[kvp.Key] = ConvertJsonValue(kvp.Value);
            }

            return normalized;
        }

        private static object? ConvertJsonValue(object value)
        {
            if (value is JsonElement element)
            {
                return element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number when element.TryGetInt64(out var l) => l,
                    JsonValueKind.Number when element.TryGetDouble(out var d) => d,
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    JsonValueKind.Object => element.ToString(),
                    JsonValueKind.Array => element.ToString(),
                    _ => element.ToString()
                };
            }

            return value;
        }

        private static OpenAIDto.ActionDto CloneAction(OpenAIDto.ActionDto action)
        {
            return new OpenAIDto.ActionDto
            {
                Action = action.Action,
                EntityType = action.EntityType,
                TargetId = action.TargetId,
                Payload = action.Payload != null
                    ? new Dictionary<string, object>(action.Payload, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            };
        }
        
        private static CreateTaskDto BuildCreateTaskDto(Dictionary<string, object> payload)
        {
            var title = GetRequiredString(payload, "title");
            var description = GetRequiredString(payload, "description");

            var startDate = GetRequiredDate(payload, "startDate");
            var dueDate = GetRequiredDate(payload, "dueDate");

            ApplyDateDefaults(ref startDate, ref dueDate);

            return new CreateTaskDto
            {
                Title = title,
                Description = description,
                StartDate = startDate,
                DueDate = dueDate
            };
        }

        private static UpdateTaskDto BuildUpdateTaskDto(Dictionary<string, object> payload)
        {
            var title = GetRequiredString(payload, "title");
            var description = GetRequiredString(payload, "description");

            var startDate = GetRequiredDate(payload, "startDate");
            var dueDate = GetRequiredDate(payload, "dueDate");

            ApplyDateDefaults(ref startDate, ref dueDate);

            return new UpdateTaskDto
            {
                Title = title,
                Description = description,
                StartDate = startDate,
                DueDate = dueDate
            };
        }

        private static string GetRequiredString(Dictionary<string, object> payload, string key)
        {
            if (payload == null || !payload.TryGetValue(key, out var value) || value == null)
            {
                throw new InvalidOperationException($"Payload missing required field '{key}'.");
            }

            var parsed = value switch
            {
                string s => s,
                _ => value.ToString() ?? string.Empty
            };

            parsed = parsed.Trim();
            if (string.IsNullOrWhiteSpace(parsed))
            {
                throw new InvalidOperationException($"Payload field '{key}' is empty.");
            }

            return parsed;
        }

        private static DateTime? GetRequiredDate(Dictionary<string, object> payload, string key)
        {
            if (payload == null || !payload.TryGetValue(key, out var value) || value == null)
            {
                return null;
            }

            var raw = value switch
            {
                string s => s,
                _ => value.ToString() ?? string.Empty
            };

            raw = raw.Trim();
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            return ParseDate(raw, key);
        }

        private static DateTime ParseDate(string raw, string key)
        {
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            {
                return dto.UtcDateTime;
            }

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dtLocal))
            {
                return DateTime.SpecifyKind(dtLocal, DateTimeKind.Local).ToUniversalTime();
            }

            throw new InvalidOperationException($"Invalid date for '{key}': {raw}");
        }

        private static void ApplyDateDefaults(ref DateTime? start, ref DateTime? due)
        {
            var todayUtc = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);

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
