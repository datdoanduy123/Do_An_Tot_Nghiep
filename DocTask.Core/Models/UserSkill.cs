namespace DocTask.Core.Models;

/// <summary>
/// Bảng ánh xạ nhiều-nhiều giữa User và Skill
/// Lưu trữ mức độ thành thạo của nhân viên với từng kỹ năng
/// </summary>
public partial class UserSkill
{
    public int UserSkillId { get; set; }
    
    public int UserId { get; set; }
    
    public int SkillId { get; set; }
    
    /// <summary>
    /// Mức độ thành thạo: 1-5
    /// 1: Beginner (Mới học)
    /// 2: Elementary (Cơ bản)
    /// 3: Intermediate (Trung bình)
    /// 4: Advanced (Nâng cao)
    /// 5: Expert (Chuyên gia)
    /// </summary>
    public int ProficiencyLevel { get; set; }
    
    /// <summary>
    /// Số năm kinh nghiệm với kỹ năng này
    /// </summary>
    public decimal? YearsOfExperience { get; set; }
    
    /// <summary>
    /// Kỹ năng được xác nhận bởi ai (UserId của người xác nhận)
    /// </summary>
    public int? VerifiedBy { get; set; }
    
    public DateTime? VerifiedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    public DateTime? UpdatedAt { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    
    public virtual Skill Skill { get; set; } = null!;
    
    public virtual User? Verifier { get; set; }
}
