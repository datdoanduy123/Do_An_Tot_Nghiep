namespace DocTask.Core.Models;

/// <summary>
/// Bảng cấu hình rule cho AI sinh task từ tài liệu.
/// Admin có thể thêm/sửa/xóa rule mà không cần sửa code.
/// 
/// RuleType có 3 loại:
/// - SkillKeyword: keyword trong nội dung TechStack → map thành skill
/// - DefaultPhase: danh sách phase mặc định khi document không có module
/// - ModuleKeywordSkill: keyword trong TÊN module → map thành skill phù hợp
/// </summary>
public class AiTaskRule
{
    public int RuleId { get; set; }

    /// <summary>
    /// Loại rule: SkillKeyword | DefaultPhase | ModuleKeywordSkill
    /// </summary>
    public string RuleType { get; set; } = null!;

    /// <summary>
    /// Keyword để match trong nội dung tài liệu hoặc tên module.
    /// VD: "ASP.NET", "REACT", "API", "GIAO DIỆN"
    /// Dùng cho SkillKeyword và ModuleKeywordSkill.
    /// Null cho DefaultPhase.
    /// </summary>
    public string? Keyword { get; set; }

    /// <summary>
    /// Tên skill sẽ được gán khi keyword match.
    /// Dùng cho SkillKeyword và ModuleKeywordSkill.
    /// VD: "ASP.NET Core", "React", "SQL Server"
    /// </summary>
    public string? SkillName { get; set; }

    /// <summary>
    /// Mức độ yêu cầu kỹ năng: 1-5.
    /// Dùng cho SkillKeyword và ModuleKeywordSkill.
    /// </summary>
    public int? RequiredLevel { get; set; }

    /// <summary>
    /// Độ quan trọng của skill: 1=Nice to have, 2=Important, 3=Critical.
    /// Dùng cho SkillKeyword và ModuleKeywordSkill.
    /// </summary>
    public int? Importance { get; set; }

    /// <summary>
    /// Tên phase sinh task mặc định.
    /// Dùng cho DefaultPhase.
    /// VD: "Phân tích & Thiết kế", "Phát triển Backend"
    /// </summary>
    public string? PhaseName { get; set; }

    /// <summary>
    /// Tỷ lệ thời gian của phase so với tổng thời gian dự án, 0.0-1.0.
    /// Dùng cho DefaultPhase. VD: 0.30 = 30%.
    /// </summary>
    public double? PhaseRatio { get; set; }

    /// <summary>
    /// Giờ ước lượng mặc định cho phase.
    /// Dùng cho DefaultPhase. VD: 80 (giờ).
    /// </summary>
    public int? PhaseHours { get; set; }

    /// <summary>
    /// Thứ tự áp dụng rule (dùng cho DefaultPhase để sắp xếp phases).
    /// </summary>
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// Rule có đang hoạt động không? false = tắt nhưng không xóa.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
