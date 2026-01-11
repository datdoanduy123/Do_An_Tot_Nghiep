namespace DocTask.Core.Models;

/// <summary>
/// Log mỗi lần phân công task
/// Dùng để track effectiveness của auto-assignment
/// </summary>
public partial class AssignmentHistory
{
    public int AssignmentHistoryId { get; set; }
    
    public int TaskId { get; set; }
    
    public int AssignedToUserId { get; set; }
    
    public int AssignedByUserId { get; set; }
    
    /// <summary>
    /// Auto (by K-means) hoặc Manual (by manager)
    /// </summary>
    public string AssignmentMethod { get; set; } = "Manual";
    
    /// <summary>
    /// Nếu auto-assigned, điểm match là bao nhiêu (0-1)
    /// </summary>
    public decimal? MatchScore { get; set; }
    
    /// <summary>
    /// Cluster ID của employee được assign (nếu auto)
    /// </summary>
    public int? ClusterIdUsed { get; set; }
    
    /// <summary>
    /// Lý do phân công (AI generated hoặc manual note)
    /// </summary>
    public string? AssignmentReason { get; set; }
    
    public DateTime AssignedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Task có được hoàn thành không? (null = chưa hoàn thành)
    /// </summary>
    public bool? IsCompleted { get; set; }
    
    /// <summary>
    /// Hoàn thành đúng hạn?
    /// </summary>
    public bool? CompletedOnTime { get; set; }
    
    public DateTime? CompletedAt { get; set; }
    
    // Navigation properties
    public virtual Task Task { get; set; } = null!;
    
    public virtual User AssignedToUser { get; set; } = null!;
    
    public virtual User AssignedByUser { get; set; } = null!;
}
