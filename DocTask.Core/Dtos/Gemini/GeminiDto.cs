using System;
using System.Collections.Generic;
using System.Linq;

namespace DocTask.Core.Dtos.Gemini
{
    public class GeminiDto
    {
        public class ApprovePlanRequest
        {
            public string CacheKey { get; set; } = "";
        }

        public class GeminiSkillRequirementDTO
        {
            public string SkillName { get; set; } = "";
            public int RequiredLevel { get; set; } = 3;
            public int Importance { get; set; } = 2;
        }

        /// <summary>
        /// Level 1: Dự án tổng thể — root node của cây Draft
        /// </summary>
        public class GeminiTaskDto
        {
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public decimal? EstimatedHours { get; set; }
            public List<GeminiSkillRequirementDTO> RequiredSkills { get; set; } = new();
            /// <summary>Danh sách Epic/Feature (Level 2)</summary>
            public List<GeminiEpicDto> Epics { get; set; } = new();
        }

        /// <summary>
        /// Level 2: Epic / Feature — nhóm các Task liên quan
        /// </summary>
        public class GeminiEpicDto
        {
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public DateTime StartDate { get; set; }
            public DateTime DueDate { get; set; }
            public decimal? EstimatedHours { get; set; }
            public List<GeminiSkillRequirementDTO> RequiredSkills { get; set; } = new();
            /// <summary>Danh sách Task cụ thể (Level 3)</summary>
            public List<GeminiSubtaskDto> Tasks { get; set; } = new();
        }

        /// <summary>
        /// Level 3: Task cụ thể cần thực hiện
        /// </summary>
        public class GeminiSubtaskDto
        {
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public DateTime StartDate { get; set; }
            public DateTime DueDate { get; set; }
            public decimal? EstimatedHours { get; set; }
            public string Priority { get; set; } = "Medium"; // Low / Medium / High
            public List<GeminiSkillRequirementDTO> RequiredSkills { get; set; } = new();
            /// <summary>Giữ lại để tương thích với code đệ quy cũ — luôn rỗng ở Level 3</summary>
            public List<GeminiSubtaskDto> Subtasks { get; set; } = new();
        }

        public class ChatRequest
        {
            public string? UserMessage { get; set; }
        }

        public class ChatResponse
        {
            public string? Response { get; set; }
            public GeminiTaskDto AiResponse { get; set; }
            public int? DraftId { get; set; }
        }

        public class GeminiOptions
        {
            public required string ApiKey { get; set; }
        }

        public enum PromptContextType
        {
            GeneralChat,           // Chat thông thường
            DatabaseQuery,         // Query từ database
            TaskSummary,           // Tổng hợp báo cáo task
            FileContentAnalysis,   // Phân tích nội dung file
            RemoveClutter,         // Loại bỏ nội dung thừa 
            GenerateTasks          // Tạo kế hoạch công việc
        }

        public static class GeminiPrompts
        {
            public const string MasterSummaryPrompt = @"
            Bạn là trợ lý AI chuyên nghiệp của hệ thống quản lý công việc DocTask.

            **Vai trò của bạn:**
            - Trợ lý thông minh hỗ trợ quản lý dự án
            - Phân tích và tổng hợp báo cáo công việc
            - Trả lời câu hỏi về dữ liệu dự án

            **Nguyên tắc:**
            1. Trả lời chính xác dựa trên dữ liệu được cung cấp
            2. Không bịa đặt thông tin không có
            3. Nếu dữ liệu mâu thuẫn, hãy nêu rõ
            4. Giữ văn phong chuyên nghiệp, súc tích

            **Cấu trúc dữ liệu:**
            {DATA_SCHEMA}

            **Nhiệm vụ:**
            {TASK_DESCRIPTION}

            **Định dạng đầu ra:**
            {OUTPUT_FORMAT}
            ";
            public const string MasterPlanPrompt = @"
            Bạn là chuyên gia lập kế hoạch dự án và phân công công việc.

            **Vai trò của bạn:**
            - Tạo danh sách tất cả công việc từ nội dung file (.docx, .pdf, .txt)
            - Bao gồm mô tả công việc, deadline
            - Không giải thích, không mô tả, không định nghĩa, chỉ tạo task

            **Nguyên tắc:**
            1. Chỉ lấy thông tin có trong file
            2. Nếu không có dữ liệu, ghi 'Chưa xác định'
            3. **Chỉ trả plain text**, không Markdown, không heading, không gạch đầu dòng, không JSON
            4. Không thêm nhận xét hay bình luận nào khác

            **Cấu trúc dữ liệu**
            {DATA_SCHEMA}

            **Loại bỏ thông tin thừa:**
            {REMOVE_CLUTTER}

            **Định dạng đầu ra:**
            {OUTPUT_FORMAT}
            ";
            
            public static string GetDataSchema(PromptContextType contextType)
            {
                return contextType switch
                {
                    PromptContextType.DatabaseQuery => @"
                    Columns trong bảng Progress:
                    - filename (nvarchar): Tên file
                    - filepath (nvarchar): Đường dẫn file
                    - proposal (nvarchar): Kế hoạch/Đề xuất
                    - result (nvarchar): Kết quả thực hiện
                    - feedback (nvarchar): Nhận xét/Phản hồi
                    Column trong bảng User:
                    - userId (int): id của người dùng
                    - username (nvarchar): tên người dùng
                    - fullName (nvarchar): tên đầy đủ của người dùng
                    - email (nvarchar): email của người dùng
                    - orgId (int): id của tổ chức của người dùng
                    - unitId (int): id của đơn vị (unit) của người dùng
                    - userParent (int): là id của người dùng cấp ngay trên người dùng đó (id người dùng cha).
                        Nếu người dùng đó cấp cao nhất, thì userParent là null
                    Column trong bảng Task:
                    - taskId (int): id của công việc
                    - title (nvarchar): tiêu đề của công việc
                    - description (nvarchar): mô tả của công việc
                    - assignerId (int): người tạo công việc
                    - startDate (DateTime): ngày bắt đầu
                    - dueDate (DateTime): ngày kết thúc
                    - parentTaskId (int): id của công việc cha. Nếu công việc là việc cha, thì parentTaskId là null
                    Column trong bảng Unit:
                    - unitId (int): id của đơn vị (unit) của người dùng
                    - userId (int): id của người dùng
                    - level (int): cấp độ
                    
                    ",

                    PromptContextType.TaskSummary => @"
                    Mỗi bản ghi báo cáo bao gồm:
                    - progressId: ID báo cáo
                    - period: Kỳ báo cáo (ngày/tuần/tháng)
                    - userName: Tên người báo cáo
                    - userId: ID người dùng
                    - updatedAt: Thời gian cập nhật
                    - status: Trạng thái (pending/in_progress/completed)
                    - proposal: Kế hoạch (từ DB)
                    - result: Kết quả (từ DB)
                    - feedback: Nhận xét (từ DB)
                    - fileName: Tên file đính kèm
                    - filePath: Đường dẫn file
                    - fullText: **QUAN TRỌNG** - Nội dung chi tiết từ file đính kèm (.docx, .pdf, .txt)",

                    PromptContextType.FileContentAnalysis => @"
                    Input là nội dung text từ các file:
                    - Nguồn: File .docx, .pdf, .txt
                    - Định dạng: Plain text hoặc formatted text
                    - Kích thước: Tối đa 20,000 ký tự mỗi file",
                    _ => "Dữ liệu dạng text tự do"
                };
            }

            public static string GetTaskDescription(PromptContextType contextType)
            {
                return contextType switch
                {
                    PromptContextType.DatabaseQuery => @"
                    - Nếu user chỉ muốn dữ liệu thô → trả về JSON đúng cấu trúc
                    - Nếu user muốn dữ liệu dễ đọc → trả về plain text",

                    PromptContextType.TaskSummary => @"
                    **QUAN TRỌNG:**
                    1. **ƯU TIÊN đọc từ `fullText`** (nội dung file đính kèm) - đây là nguồn dữ liệu chính
                    2. Chỉ dùng proposal/result/feedback từ DB nếu fullText trống
                    3. Gộp nội dung từ TẤT CẢ thành viên thành một báo cáo duy nhất
                    4. Loại bỏ thông tin trùng lặp
                    5. Sắp xếp theo thứ tự logic
                    6. Phân tích xu hướng và đưa ra đề xuất",

                    PromptContextType.FileContentAnalysis => @"
                    - Đọc và phân tích nội dung file
                    - Trích xuất thông tin quan trọng
                    - Tóm tắt ngắn gọn",
                    PromptContextType.GenerateTasks => @"
                    - 
                    ",
                    _ => "Trả lời câu hỏi của người dùng"
                };
            }

            public static string GetOutputFormat(PromptContextType contextType)
            {
                return contextType switch
                {
                    PromptContextType.DatabaseQuery => @"
                    - Nếu người dùng yêu cầu dữ liệu JSON, phải trả về JSON hợp lệ không được bọc string
                    - Nếu người dùng cần phân tích, trả về text thuần (plaintext)
                    ",

                    PromptContextType.TaskSummary => @"
                    **KHÔNG dùng markdown code block (```markdown)**

                    Trả về theo cấu trúc:

                    BÁO CÁO TỔNG HỢP - [TÊN TASK]

                    I. TỔNG QUAN DỰ ÁN
                    - Thời gian: [Từ - đến]
                    - Số thành viên: [X người]
                    - Tình trạng: [Đúng tiến độ/Chậm/Vượt]

                    II. KẾ HOẠCH & ĐỀ XUẤT
                    2.1. Mục tiêu
                    [Gộp từ fullText của tất cả báo cáo]

                    2.2. Phương pháp
                    [Tổng hợp phương pháp]

                    III. KẾT QUẢ ĐẠT ĐƯỢC
                    3.1. Đã hoàn thành
                    [Từ fullText]

                    3.2. Đang thực hiện
                    [Từ fullText]

                    IV. VẤN ĐỀ & PHẢN HỒI
                    4.1. Khó khăn
                    [Từ fullText]

                    4.2. Đề xuất
                    [Từ fullText]

                    V. ĐÁNH GIÁ & KẾ HOẠCH TIẾP THEO
                    5.1. Đánh giá
                    [Phân tích tổng thể]

                    5.2. Việc cần làm
                    [Công việc tiếp theo]

                    **Độ dài:** 800-1500 từ
                    **Văn phong:** Chuyên nghiệp, rõ ràng
                    Lưu ý: 
                    Văn bản dùng font chữ Times New Roman
                    Các mục I, II, III,... dùng cỡ chữ 15, in đậm
                    Các mục 1, 2, 3,... dùng cỡ chữ 12, in đậm
                    ",

                    PromptContextType.GenerateTasks => @"
Bạn là AI chuyên gia lập kế hoạch dự án. Phân tích tài liệu và sinh cấu trúc công việc 3 cấp.

QUY TẮC TUYỆT ĐỐI:
- CHỈ trả về JSON hợp lệ, KHÔNG giải thích, KHÔNG markdown, KHÔNG text ngoài JSON
- CHỈ dùng thông tin từ tài liệu; nếu thiếu thì suy luận hợp lý

CẤU TRÚC 3 CẤP:
  Level 1 (project): Dự án tổng thể
  Level 2 (epics):   3–8 Epic/Feature nhóm các chức năng
  Level 3 (tasks):   3–10 Task cụ thể trong mỗi Epic

QUY TẮC THỜI GIAN:
- startDate <= dueDate (KHÔNG đảo ngược)
- Nếu không có ngày: startDate = ngày hiện tại, dueDate = startDate + 14 ngày
- project.endDate = endDate của Epic cuối cùng

QUY TẮC GIỜ (estimatedHours):
- Task nhỏ: 4–16 giờ | Task trung bình: 16–40 giờ | Task lớn: 40–80 giờ
- Epic.estimatedHours = tổng tasks trong epic
- project.estimatedHours = tổng tất cả epics

QUY TẮC SKILL:
- project: 5–8 skill TỔNG QUÁT (vd: Backend Development, Frontend Development)
- epic: 3–5 skill TRUNG BÌNH
- task: 2–4 skill CỤ THỂ (vd: C# .NET, ReactJS, SQL Server)
- requiredLevel: 1=Beginner 2=Elementary 3=Intermediate 4=Advanced 5=Expert
- importance: 1=Nice-to-have 2=Important 3=Critical
- KHÔNG trùng skillName trong cùng node

Nếu tài liệu KHÔNG có chức năng rõ ràng, BẮT BUỘC tạo epics mặc định:
  Epic 1: Phân tích & Thiết kế hệ thống
  Epic 2: Phát triển Backend API
  Epic 3: Phát triển Frontend
  Epic 4: Kiểm thử & Triển khai

ĐỊNH DẠNG JSON BẮT BUỘC (KHÔNG thay đổi tên field):
{
  ""title"": ""Tên dự án"",
  ""description"": ""Mô tả dự án"",
  ""startDate"": ""yyyy-MM-dd"",
  ""endDate"": ""yyyy-MM-dd"",
  ""estimatedHours"": 0,
  ""requiredSkills"": [{""skillName"": """", ""requiredLevel"": 3, ""importance"": 2}],
  ""epics"": [
    {
      ""title"": ""Epic: Tên tính năng"",
      ""description"": ""Mô tả epic"",
      ""startDate"": ""yyyy-MM-dd"",
      ""dueDate"": ""yyyy-MM-dd"",
      ""estimatedHours"": 0,
      ""requiredSkills"": [{""skillName"": """", ""requiredLevel"": 3, ""importance"": 2}],
      ""tasks"": [
        {
          ""title"": ""Tên task cụ thể"",
          ""description"": ""Mô tả task"",
          ""startDate"": ""yyyy-MM-dd"",
          ""dueDate"": ""yyyy-MM-dd"",
          ""estimatedHours"": 8,
          ""priority"": ""Medium"",
          ""requiredSkills"": [{""skillName"": """", ""requiredLevel"": 3, ""importance"": 2}]
        }
      ]
    }
  ]
}
",

                    _ => "Trả lời tự nhiên, dễ hiểu",
                };
            }

            public static string GetRemoveClutter(PromptContextType contextType)
            {
                return contextType switch
                {
                    PromptContextType.RemoveClutter => @"
                    Bạn cần phải loại bỏ các thông tin không cần thiết trong văn bản. Bạn làm theo quy trình như sau:
                    1. Đọc toàn bộ văn bản
                    2. Xác định những từ hay thuật ngữ mang tính chất hoa mỹ (ví dụ: chuyển đổi số, công nghệ AI, bứt phá, dễ dàng...)
                    - Nếu như thuật ngữ đó có thể lượng hóa được hay gợi ra chú ý liên quan đến công việc, hãy giữ lại.
                    - Nếu như thuật ngữ chỉ mang tính chất miêu tả định tính, cảm xúc, không lượng hóa được, thì loại bỏ.
                    3. Nhận diện các công việc/ nhiệm vụ qua những động từ như 'Triển khai…', 'Áp dụng…', 'Xây dựng…', 'Sử dụng…', 'Khuyến khích…'.
                    4. Xác định các thuật ngữ về thời gian, kỳ báo cáo. Nếu có những thuật ngữ như 'càng sớm càng tốt', đánh dấu cả ngày bắt đầu và ngày kết thúc là
                    không xác định.  
                ",
                    _ => "Yêu cầu đầu ra thành văn bản đầy đủ, không bị thiếu sót hay thừa"
                };
            }
        }
    }
}
