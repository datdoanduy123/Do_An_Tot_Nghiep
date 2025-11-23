using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Tasks;

namespace DocTask.Core.Dtos.Gemini
{
    public class GeminiDto
    {
        public class ApprovePlanRequest
        {
            public string CacheKey { get; set; } = "";
        }

        public class GeminiTaskDto
        {
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
            public List<GeminiSubtaskDto> Subtasks { get; set; } = new();
        }

        public class GeminiSubtaskDto
        {
            public string Title { get; set; } = "";
            public string Description { get; set; } = "";
            public DateTime StartDate { get; set; }
            public DateTime DueDate { get; set; }
            public string Frequency { get; set; } = "daily"; // daily/weekly/monthly
            public List<int> AssignedUserIds { get; set; } = new();
            public List<int> AssignedUnitIds { get; set; } = new();
        }

        public class ChatRequest
        {
            public string? UserMessage { get; set; }
        }

        public class ChatResponse
        {
            public string? Response { get; set; }
            public GeminiTaskDto AiResponse { get; set; }
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
                    Trích xuất tất cả các công việc từ nội dung văn bản. 
                    Lấy tiêu đề của tài liệu làm tên việc cha, và các công việc trong đó là việc con
                    - Mỗi công việc, kể cả việc cha hay việc con, đều bao gồm các thông tin sau: 
                        + Tên công việc
                        + Mô tả công việc
                            * Chi tiết mô tả
                        + Ngày bắt đầu
                        + Ngày kết thúc (deadline)
                    - Nếu văn bản KHÔNG có ngày bắt đầu → dùng ngày hiện tại ({DateTime.Now:dd/MM/yyyy})
                    - Nếu văn bản KHÔNG có ngày kết thúc → lấy sau 45 ngày ({DateTime.Now.AddDays(45):dd/MM/yyyy})
                    - Mọi task cần phải có startDate, endDate
                    Trả theo cấu trúc JSON như sau:

                    {
                        ""title"": ""[Tên dự án]"",
                        ""description"": ""[Mô tả dự án cha]"",
                        ""startDate"": ""[dd/MM/yyyy]"",
                        ""endDate"": ""[dd/MM/yyyy]"",
                        ""subtasks"": [
                            {
                            ""title"": ""[Tên công việc]"",
                            ""description"": ""[Mô tả chi tiết]"",
                            ""startDate"": ""[dd/MM/yyyy]"",
                            ""dueDate"": ""[dd/MM/yyyy]"",
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
