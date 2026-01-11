namespace DocTask.Core.Models;

/// <summary>
/// Hồ sơ năng lực tổng hợp của nhân viên
/// Dùng làm input cho K-means clustering
/// </summary>
public partial class EmployeeProfile
{
    public int EmployeeProfileId { get; set; }
    
    public int UserId { get; set; }
    
    /// <summary>
    /// Tổng số năm kinh nghiệm làm việc
    /// </summary>
    public decimal TotalYearsOfExperience { get; set; }
    
    /// <summary>
    /// Điểm năng lực trung bình (1-5)
    /// Tính từ trung bình ProficiencyLevel của tất cả skills
    /// </summary>
    public decimal AverageSkillLevel { get; set; }
    
    /// <summary>
    /// Điểm năng suất (productivity score)
    /// Được tính từ lịch sử hoàn thành task
    /// VD: Số task hoàn thành đúng hạn / Tổng số task
    /// Giá trị từ 0.0 đến 1.0
    /// </summary>
    public decimal ProductivityScore { get; set; }
    
    /// <summary>
    /// Tải công việc hiện tại (0-100%)
    /// </summary>
    public decimal CurrentWorkloadPercentage { get; set; }
    
    /// <summary>
    /// Số giờ khả dụng còn lại trong tuần
    /// VD: 40 giờ (full-time), 20 giờ (part-time)
    /// </summary>
    public decimal AvailableHoursPerWeek { get; set; }
    
    /// <summary>
    /// Điểm đánh giá hiệu suất trung bình (1-5)
    /// </summary>
    public decimal? AveragePerformanceRating { get; set; }
    
    /// <summary>
    /// Số task đã hoàn thành
    /// </summary>
    public int CompletedTasksCount { get; set; } = 0;
    
    /// <summary>
    /// Số task hoàn thành đúng hạn
    /// </summary>
    public int OnTimeCompletionCount { get; set; } = 0;
    
    /// <summary>
    /// Cập nhật lần cuối
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Trạng thái nhân viên: Active, OnLeave, Busy
    /// </summary>
    public string Status { get; set; } = "Active";
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
}
