using System;
using System.Collections.Generic;

namespace DocTask.Core.Models;

/// <summary>
/// Lưu trữ các task được sinh ra bởi AI (Draft)
/// Chờ người dùng duyệt trước khi chuyển thành Task chính thức
/// </summary>
public partial class TaskDraft
{
    public int DraftId { get; set; }

    /// <summary>
    /// Self-referencing: ID của draft cha (nếu là subtask)
    /// </summary>
    public int? ParentDraftId { get; set; }

    public int FileId { get; set; }

    public int CreatedBy { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public decimal? EstimatedHours { get; set; }

    /// <summary>
    /// Raw JSON response từ AI để backup/debug
    /// </summary>
    public string? RawAIResponse { get; set; }

    /// <summary>
    /// Trạng thái: Pending, Approved, Rejected
    /// </summary>
    public string Status { get; set; } = "Pending";

    public int? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewNotes { get; set; }

    /// <summary>
    /// ID của task thật sau khi được approve
    /// </summary>
    public int? CreatedTaskId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual TaskDraft? ParentDraft { get; set; }
    public virtual ICollection<TaskDraft> InverseParentDraft { get; set; } = new List<TaskDraft>();

    public virtual Uploadfile File { get; set; } = null!;
    public virtual User Creator { get; set; } = null!;
    public virtual User? Reviewer { get; set; }
    public virtual Task? CreatedTask { get; set; }

    public virtual ICollection<TaskDraftSkill> DraftSkills { get; set; } = new List<TaskDraftSkill>();
}
