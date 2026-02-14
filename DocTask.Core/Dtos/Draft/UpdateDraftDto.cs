using System;

namespace DocTask.Core.Dtos.Draft
{
    /// <summary>
    /// DTO cho phép sửa draft trước khi Approve.
    /// PATCH semantics: null = không thay đổi field đó.
    /// Dùng cho PATCH /drafts/{draftId}
    /// </summary>
    public class UpdateDraftDto
    {
        /// <summary>Tiêu đề mới (null = giữ nguyên)</summary>
        public string? Title { get; set; }

        /// <summary>Mô tả mới (null = giữ nguyên)</summary>
        public string? Description { get; set; }

        /// <summary>Ngày bắt đầu mới (null = giữ nguyên)</summary>
        public DateTime? StartDate { get; set; }

        /// <summary>Ngày kết thúc mới (null = giữ nguyên)</summary>
        public DateTime? EndDate { get; set; }

        /// <summary>Số giờ ước lượng mới (null = giữ nguyên)</summary>
        public decimal? EstimatedHours { get; set; }
    }
}
