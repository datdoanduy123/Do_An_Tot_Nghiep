namespace DocTask.Core.Models;

/// <summary>
/// Snapshot của workload tại một thời điểm
/// Dùng để track lịch sử và visualize workload trends
/// </summary>
public partial class WorkloadMetric
{
    public int WorkloadMetricId { get; set; }
    
    public int UserId { get; set; }
    
    /// <summary>
    /// Số task đang active
    /// </summary>
    public int ActiveTasksCount { get; set; }
    
    /// <summary>
    /// Tổng số giờ ước tính còn lại
    /// </summary>
    public decimal EstimatedHoursRemaining { get; set; }
    
    /// <summary>
    /// Phần trăm workload (0-100)
    /// </summary>
    public decimal WorkloadPercentage { get; set; }
    
    /// <summary>
    /// Số giờ available
    /// </summary>
    public decimal AvailableHours { get; set; }
    
    /// <summary>
    /// Ngày snapshot
    /// </summary>
    public DateTime SnapshotDate { get; set; } = DateTime.Now;
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
}
