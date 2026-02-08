using System;
using System.Collections.Generic;

namespace DocTask.Core.Models;

/// <summary>
/// Lưu trữ yêu cầu kỹ năng cho Task Draft
/// </summary>
public partial class TaskDraftSkill
{
    public int DraftSkillId { get; set; }

    public int DraftId { get; set; }

    public int SkillId { get; set; }

    /// <summary>
    /// Mức độ yêu cầu: 1-5
    /// </summary>
    public int RequiredLevel { get; set; }

    /// <summary>
    /// Độ quan trọng: 1-3
    /// </summary>
    public int Importance { get; set; } = 2;

    /// <summary>
    /// True nếu được AI extract, False nếu do user thêm tay
    /// </summary>
    public bool IsAIExtracted { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual TaskDraft Draft { get; set; } = null!;
    public virtual Skill Skill { get; set; } = null!;
}
