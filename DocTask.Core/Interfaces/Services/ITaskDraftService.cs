using DocTask.Core.Dtos.Draft;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Services
{
    /// <summary>
    /// Service xử lý Draft (bản nháp task từ AI).
    /// Quản lý vòng đời: Tạo → Review → Edit → Approve/Reject
    /// </summary>
    public interface ITaskDraftService
    {
        // ========== TẠO DRAFT ==========

        /// <summary>
        /// Tạo Draft từ dữ liệu AI (Json hoặc DTO)
        /// </summary>
        Task<int> CreateDraftFromGeminiAsync(object geminiTaskData, int fileId, int userId);

        // ========== XEM / REVIEW ==========

        /// <summary>
        /// Lấy danh sách root drafts theo fileId
        /// </summary>
        Task<List<TaskDraft>> GetDraftsByFileIdAsync(int fileId);

        /// <summary>
        /// Lấy raw draft tree (model gốc)
        /// </summary>
        Task<TaskDraft?> GetDraftTreeAsync(int draftId);

        /// <summary>
        /// Lấy draft tree dưới dạng DTO flatten (có skills, subtasks)
        /// Dùng cho màn Review trước khi Approve
        /// </summary>
        Task<DraftTreeDto?> GetDraftTreeDtoAsync(int draftId);

        // ========== EDIT DRAFT ==========

        /// <summary>
        /// Cập nhật draft (title, desc, date, hours).
        /// PATCH semantics: null = không thay đổi.
        /// Chỉ cho phép khi Status = "Pending"
        /// </summary>
        Task<DraftTreeDto> UpdateDraftAsync(int draftId, UpdateDraftDto dto, int userId);

        /// <summary>
        /// Xóa subtask draft.
        /// Bảo vệ: không cho xóa root (ParentDraftId == null).
        /// Bảo vệ: không cho xóa nếu Status != "Pending"
        /// </summary>
        System.Threading.Tasks.Task DeleteSubDraftAsync(int subDraftId, int userId);

        // ========== DUYỆT / TỪ CHỐI ==========

        /// <summary>
        /// Approve draft → tạo Task thật trong bảng Tasks.
        /// Validate: timeline, title, EstimatedHours.
        /// Timeline clamp cho subtasks.
        /// </summary>
        Task<TaskDto> ApproveDraftAsync(int draftId, int userId);

        /// <summary>
        /// Reject draft — lưu lý do để audit.
        /// Đánh status cả cây = "Rejected"
        /// </summary>
        System.Threading.Tasks.Task RejectDraftAsync(int draftId, int userId, string? reason = null);
    }
}