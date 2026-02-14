using System;
using System.Collections.Generic;

namespace DocTask.Core.Dtos.Draft
{
    // ================================================================
    //  DraftTreeDto — Response DTO cho cây draft (root + subtasks + skills)
    //  Dùng cho GET /drafts/{id}/tree
    // ================================================================

    /// <summary>
    /// DTO trả về cây draft đầy đủ: root draft + subtasks + skills flatten.
    /// Dùng cho màn Review trước khi Approve.
    /// </summary>
    public class DraftTreeDto
    {
        /// <summary>ID của draft</summary>
        public int DraftId { get; set; }

        /// <summary>ID file gốc đã upload</summary>
        public int FileId { get; set; }

        /// <summary>Người tạo draft</summary>
        public int CreatedBy { get; set; }

        /// <summary>Tiêu đề dự án</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>Mô tả dự án</summary>
        public string? Description { get; set; }

        /// <summary>Trạng thái: Pending / Approved / Rejected</summary>
        public string Status { get; set; } = "Pending";

        /// <summary>Ngày bắt đầu dự kiến</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>Ngày kết thúc dự kiến</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>Số giờ ước lượng</summary>
        public decimal? EstimatedHours { get; set; }

        /// <summary>ID của task thật (sau khi Approved)</summary>
        public int? CreatedTaskId { get; set; }

        /// <summary>Lý do reject (nếu bị từ chối)</summary>
        public string? ReviewNotes { get; set; }

        /// <summary>Ngày tạo draft</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>Ngày review</summary>
        public DateTime? ReviewedAt { get; set; }

        /// <summary>Danh sách kỹ năng yêu cầu của task cha</summary>
        public List<DraftSkillDto> Skills { get; set; } = new();

        /// <summary>Danh sách subtasks con</summary>
        public List<DraftSubtaskDto> Subtasks { get; set; } = new();
    }

    /// <summary>
    /// Kỹ năng yêu cầu của một draft (flatten từ TaskDraftSkill + Skill)
    /// </summary>
    public class DraftSkillDto
    {
        public int SkillId { get; set; }

        /// <summary>Tên kỹ năng (vd: "C# .NET", "React")</summary>
        public string SkillName { get; set; } = string.Empty;

        /// <summary>Mức yêu cầu: 1-5</summary>
        public int RequiredLevel { get; set; }

        /// <summary>Độ quan trọng: 1-3</summary>
        public int Importance { get; set; }
    }

    /// <summary>
    /// Subtask con trong cây draft
    /// </summary>
    public class DraftSubtaskDto
    {
        public int DraftId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? EndDate { get; set; }

        public decimal? EstimatedHours { get; set; }

        public string Status { get; set; } = "Pending";

        /// <summary>Kỹ năng yêu cầu riêng cho subtask này</summary>
        public List<DraftSkillDto> Skills { get; set; } = new();

        /// <summary>Danh sách subtasks con (Work Packages)</summary>
        public List<DraftSubtaskDto> Subtasks { get; set; } = new();
    }
}
