namespace DocTask.Core.Models;

/// <summary>
/// Danh mục các kỹ năng trong công ty
/// VD: Java, C#, React, Docker, Database Design...
/// </summary>
public partial class Skill
{
    public int SkillId { get; set; }
    
    /// <summary>
    /// Tên kỹ năng - VD: "Java Programming", "C# .NET", "React"
    /// </summary>
    public string SkillName { get; set; } = null!;
    
    /// <summary>
    /// Danh mục kỹ năng - VD: "Backend", "Frontend", "DevOps", "Database"
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Mô tả chi tiết về kỹ năng
    /// </summary>
    public string? Description { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Trạng thái hoạt động - dùng cho soft delete
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    // Navigation properties
    public virtual ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
    
    public virtual ICollection<TaskSkillRequirement> TaskSkillRequirements { get; set; } = new List<TaskSkillRequirement>();
}
