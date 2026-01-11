namespace DocTask.Core.Models;

/// <summary>
/// Yêu cầu kỹ năng cho mỗi task/subtask
/// Được extract tự động từ AI hoặc manual input
/// </summary>
public partial class TaskSkillRequirement
{
    public int TaskSkillRequirementId { get; set; }
    
    public int TaskId { get; set; }
    
    public int SkillId { get; set; }
    
    /// <summary>
    /// Mức độ yêu cầu tối thiểu: 1-5
    /// </summary>
    public int RequiredLevel { get; set; }
    
    /// <summary>
    /// Độ quan trọng của kỹ năng này trong task
    /// 1: Nice to have
    /// 2: Important
    /// 3: Critical (Must have)
    /// </summary>
    public int Importance { get; set; } = 2;
    
    /// <summary>
    /// Được extract tự động từ AI hay manual?
    /// </summary>
    public bool IsAutoExtracted { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Navigation properties
    public virtual Task Task { get; set; } = null!;
    
    public virtual Skill Skill { get; set; } = null!;
}
