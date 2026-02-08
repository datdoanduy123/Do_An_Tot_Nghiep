using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Services
{
    public interface ITaskDraftService
    {
        // Tạo Draft từ dữ liệu AI (Json hoặc DTO)
        Task<int> CreateDraftFromGeminiAsync(object geminiTaskData, int fileId, int userId);

        // Lấy danh sách
        Task<List<TaskDraft>> GetDraftsByFileIdAsync(int fileId);
        Task<TaskDraft?> GetDraftTreeAsync(int draftId);

        // Duyệt / Từ chối
        Task<TaskDto> ApproveDraftAsync(int draftId, int userId);
        System.Threading.Tasks.Task RejectDraftAsync(int draftId, int userId, string? reason = null);
    }
}