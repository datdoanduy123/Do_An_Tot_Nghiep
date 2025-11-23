using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace DocTask.Core.Dtos.OpenAIDto
{
    public class OpenAIDto
    {
        public class Model
        {
            public const string Chat_v4o_Mini = "gpt-4o-mini";
            public const string Chat_v41_Nano = "gpt-4.1-nano";
            public const string Chat_v41_Mini = "gpt-4.1-mini";
        }

        public class RequestDto
        {
            public string Prompt { get; set; } = "";
        }

        public class ResponseDto
        {
            public string Response { get; set; } = "";
        }

        public class AgentRequestDto
        {
            /// <summary>
            /// Prompt chính cho agent.
            /// </summary>
            public string? Prompt { get; set; } = "";

            /// <summary>
            /// Danh sách ID File muốn AI đọc.
            /// </summary>
            public List<int> FileIds { get; set; } = new();
            
            /// <summary>
            /// Có chạy execute luôn sau khi phân tích hay không.
            /// </summary>
            public bool AutoExecute { get; set; } = true;
        }

        public class ActionDto
        {
            [Required]
            [RegularExpression("create|update|delete",
            ErrorMessage = "Action must be 'create|update|delete'.")]
            public string Action { get; set; } = "";

            [Required]
            [RegularExpression("task|subtask",
            ErrorMessage = "Only 'task|subtask' entityType is supported.")]
            public string EntityType { get; set; } = "";

            /// <summary>
            /// Required khi update|delete
            /// </summary>
            public int? TargetId { get; set; }

            public Dictionary<string, object> Payload { get; set; } = new();
        }

        public class FileContextDto
        {
            public int FileId { get; set; }
            public string FileName { get; set; } = "";
            public string? Preview { get; set; } = "";
        }

        public class ListActionDto
        {
            public List<ActionDto> ListAction { get; set; } = new();
            public string RawResponse { get; set; } = "";
            public List<FileContextDto> ContextFiles { get; set; } = new();
            public List<int> MissingFileIds { get; set; } = new();
        }

        public class ActionExecutionResultDto
        {
            public ActionDto Action { get; set; } = new();
            public bool Success { get; set; }
            public string Message { get; set; } = "";
            public object? Output { get; set; }
        }

        public class AgentExecutionResultDto
        {
            public List<ActionDto> PlannedActions { get; set; } = new();
            public List<ActionExecutionResultDto> ExecutedActions { get; set; } = new();
            public List<FileContextDto> ContextFiles { get; set; } = new();
            public List<int> MissingFileIds { get; set; } = new();
            public string RawModelOutput { get; set; } = "";
        }

        public static class Prompts
        {
            public const string FileAssistant = @"
                Bạn là một trợ lý AI chuyên lập báo cáo tổng hợp chuyên nghiệp. 
                Bạn sẽ nhận nhiều báo cáo con từ nhiều file khác nhau. 
                Hãy phân tích, tổng hợp và xuất ra một báo cáo duy nhất, 
                theo đúng cấu trúc chuẩn sau:

                BÁO CÁO TỔNG HỢP

                1. Giới thiệu
                - Bối cảnh và phạm vi báo cáo

                2. Tổng quan dữ liệu
                - Số lượng báo cáo con
                - Các nguồn dữ liệu (tóm tắt)

                3. Các phát hiện chính
                - Gạch đầu dòng hoặc phân mục

                4. Kết quả đạt được
                - Tóm tắt những gì đã hoàn thành
                - Các chỉ số nổi bật (nếu có)

                5. Vấn đề tồn tại
                - Những hạn chế, khó khăn, rủi ro

                6. Đề xuất & Kiến nghị
                - Các giải pháp, hành động cần thực hiện

                7. Kết luận
                - Tóm gọn nội dung báo cáo
                - Bước tiếp theo

                Yêu cầu:
                - Trình bày rõ ràng, súc tích
                - Giữ ngôn ngữ khách quan, không thêm thông tin không có trong báo cáo gốc
                - Báo cáo ra ở dạng văn bản hoàn chỉnh, có thể gửi trực tiếp cho cấp quản lý
            ";

            public const string SummaryAssistant = @"
                Bạn là trợ lý AI chuyên phân tích và lập báo cáo dự án. 
                Bạn sẽ được cung cấp:
                - Dữ liệu từ database (ở dạng JSON hoặc text)
                - Báo cáo tổng hợp từ các file báo cáo

                Nhiệm vụ của bạn:
                1. Đọc toàn bộ nội dung hai nguồn dữ liệu.
                2. KHÔNG được chỉnh sửa, thêm hoặc suy đoán về dữ liệu lấy từ database Proposal, Result, Feedback. Những phần này đã có sẵn từ dữ liệu database.
                3. Với Proposal, Result, Feedback, dữ liệu được lấy từ database phải giữ nguyên nhưng có thể thêm các nội dung khác cho phù hợp với ngữ cảnh.
                4. KHÔNG được chỉnh sửa, thêm hoặc suy đoán về Nội dung tổng hợp. Những phần này đã có sẵn từ báo cáo tổng hợp từ các file báo cáo.
                5. Xuất ra một báo cáo cuối cùng với 4 phần:

                BÁO CÁO DỰ ÁN

                1. Proposal (Đề xuất)
                - (lấy từ database)

                2. Result (Kết quả đạt được)
                - (lấy từ database)

                3. Feedback (Phản hồi / nhận xét)
                - (lấy từ database)

                4. Nội dung tổng hợp
                - (lấy từ *Báo cáo tổng hợp*)

                Yêu cầu:
                - Viết ngắn gọn, súc tích, logic
                - Không được bỏ sót thông tin quan trọng
                - Báo cáo phải sẵn sàng để gửi cho quản lý
            ";

            public const string TaskAutomationAgent = @"
                You are a task automation agent that converts user goals and document context into Task API
                operations. Respond with a JSON array only, no markdown or prose.

                Each action creates a parent task. A parent task may have zero, one, or many child subtasks (use an empty
                array when no child job is needed or no information is available). Subtasks are the smaller jobs executed
                by specific people or units that contribute to the parent task.

                Action schema:
                {
                    ""action"": ""create"",
                    ""entityType"": ""task"",
                    ""payload"":
                    {
                        ""title"": ""..."",
                        ""description"": ""..."",
                        ""startDate"": ""ISO 8601"",
                        ""dueDate"": ""ISO 8601"",
                        ""assignedUserIds"": [...],
                        ""assignedUnitIds"": [...],
                        ""subtasks"": [
                            {
                                ""title"": ""..."",
                                ""description"": ""..."",
                                ""startDate"": ""ISO 8601"",
                                ""dueDate"": ""ISO 8601"",
                                ""frequency"": ""daily | weekly | monthly"",    // optional 
                                ""intervalValue"": ""1 | 2 | 3"",               // optional 
                                ""days"": [...],                // optional array
                                ""assignedUserIds"": [...],
                                ""assignedUnitIds"": [...], 
                            }
                        ]
                    }
                }

                Rules:
                - Treat the parent ""task"" as the overarching job. ""subtasks"" may be zero, one, or many child jobs. Use [] only when no child work is required.
                - Documents may be in Vietnamese; read them in the original language and extract tasks/subtasks accordingly.
                - If the document hints at multiple steps or responsibilities, generate one or more subtasks. Otherwise [] is acceptable.
                - Use only data present in the prompt or supplied documents; never invent facts or IDs.
                - If no start date is given, set startDate to today's UTC date.
                - If no due date is given, set dueDate to startDate or startDate + 7 days when a longer window is implied.
                - Ensure dueDate >= startDate.
                - Skip actions when mandatory information (title) is missing.
                - Trim redundant whitespace; create concise descriptions.
                - Always respond with a JSON array of actions (even if empty). Do not wrap the array inside another object.
                - Never include additional text or explanations outside the JSON array.
            ";
        }
    }
}
