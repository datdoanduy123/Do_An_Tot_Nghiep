namespace DocTask.Core.Dtos.AiGeneration;

/// <summary>
/// DTO kết quả bóc tách nội dung tài liệu yêu cầu theo template chuẩn.
/// Được sinh ra bởi DocumentTemplateParser (deterministic, không dùng AI),
/// đảm bảo không bị LLM "bịa" và dễ debug.
/// </summary>
public class DocumentExtractDto
{
    // ────────────────────────────────────────────
    // Thông tin chung dự án
    // ────────────────────────────────────────────

    /// <summary>Tên dự án (VD: "Hệ thống quản lý bán hàng")</summary>
    public string ProjectName { get; set; } = "Dự án mới";

    /// <summary>Ngày bắt đầu dự án, null nếu không parse được</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Ngày kết thúc dự án, null nếu không parse được</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Ngân sách (chuỗi nguyên văn từ tài liệu)</summary>
    public string? Budget { get; set; }

    /// <summary>Mô tả tổng quan dự án (phần "MÔ TẢ DỰ ÁN")</summary>
    public string? ProjectDescription { get; set; }

    // ────────────────────────────────────────────
    // Tech stack
    // ────────────────────────────────────────────

    /// <summary>
    /// Danh sách công nghệ parse được từ phần "Công nghệ sử dụng:".
    /// VD: ["ASP.NET Core 8", "ReactJS", "SQL Server", "Docker"]
    /// </summary>
    public List<string> TechStack { get; set; } = new();

    // ────────────────────────────────────────────
    // Danh sách chức năng
    // ────────────────────────────────────────────

    /// <summary>
    /// Danh sách các chức năng lớn (block "CHỨC NĂNG X: ...").
    /// Mỗi chức năng tương ứng 1 Epic trong Agile plan.
    /// </summary>
    public List<FunctionExtractDto> Functions { get; set; } = new();

    // ────────────────────────────────────────────
    // Yêu cầu phi chức năng
    // ────────────────────────────────────────────

    /// <summary>
    /// Danh sách yêu cầu phi chức năng (NFR) từ phần "YÊU CẦU PHI CHỨC NĂNG".
    /// VD: ["API response < 200ms", "Uptime 99.9%"]
    /// </summary>
    public List<string> NonFunctionalRequirements { get; set; } = new();

    // ────────────────────────────────────────────
    // Metadata
    // ────────────────────────────────────────────

    /// <summary>Tên file gốc đã upload</summary>
    public string? SourceFileName { get; set; }

    /// <summary>Cảnh báo phát sinh trong quá trình parse (VD: "Không tìm thấy thời gian")</summary>
    public List<string> ParseWarnings { get; set; } = new();
}

/// <summary>
/// Mô tả 1 chức năng lớn trong tài liệu (block "CHỨC NĂNG X: ...").
/// Tương ứng với 1 Epic trong Agile plan.
/// </summary>
public class FunctionExtractDto
{
    /// <summary>Số chức năng (VD: 1, 2, 3…)</summary>
    public int FunctionNumber { get; set; }

    /// <summary>Tên chức năng (VD: "QUẢN LÝ NGƯỜI DÙNG & XÁC THỰC")</summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>Mô tả tổng quan chức năng</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Danh sách yêu cầu chi tiết (dòng "X.Y ...").
    /// Mỗi dòng tương ứng 1 Story trong Agile plan.
    /// VD: ["Đăng ký tài khoản (email, số điện thoại)", "Đăng nhập bằng email/mật khẩu"]
    /// </summary>
    public List<string> Details { get; set; } = new();

    /// <summary>Deadline của chức năng (parse từ "Thời gian: DD/MM/YYYY - DD/MM/YYYY")</summary>
    public DateTime? Deadline { get; set; }

    /// <summary>Thời gian bắt đầu chức năng (parse từ "Thời gian:")</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Ước tính số giờ (parse từ "Ước tính: X giờ")</summary>
    public decimal? EstimatedHours { get; set; }
}
