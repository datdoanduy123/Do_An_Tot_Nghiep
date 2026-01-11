namespace DocTask.Core.Models;

/// <summary>
/// Đánh giá định kỳ hiệu suất nhân viên
/// Có thể dùng để cập nhật EmployeeProfile
/// </summary>
public partial class PerformanceReview
{
    public int PerformanceReviewId { get; set; }
    
    public int UserId { get; set; }
    
    public int ReviewedByUserId { get; set; }
    
    /// <summary>
    /// Kỳ đánh giá: Q1 2026, Q2 2026...
    /// </summary>
    public string ReviewPeriod { get; set; } = null!;
    
    /// <summary>
    /// Điểm tổng thể (1-5)
    /// </summary>
    public decimal OverallRating { get; set; }
    
    /// <summary>
    /// Điểm kỹ thuật (1-5)
    /// </summary>
    public decimal TechnicalSkillsRating { get; set; }
    
    /// <summary>
    /// Điểm làm việc nhóm (1-5)
    /// </summary>
    public decimal TeamworkRating { get; set; }
    
    /// <summary>
    /// Điểm đúng deadline (1-5)
    /// </summary>
    public decimal TimelinessRating { get; set; }
    
    public string? Comments { get; set; }
    
    public DateTime ReviewDate { get; set; } = DateTime.Now;
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    
    public virtual User ReviewedBy { get; set; } = null!;
}
