using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Repositories
{
    public interface ITaskDraftRepository
    {
        // Nghiệp vụ Draft
        Task<List<TaskDraft>> GetByFileIdAsync(int fileId);
        Task<TaskDraft?> GetByIdWithChildrenAsync(int id);

        // CRUD cơ bản
        Task<TaskDraft> CreateAsync(TaskDraft draft);
        Task<TaskDraft?> GetByIdAsync(int id);
        System.Threading.Tasks.Task UpdateAsync(TaskDraft draft);
        System.Threading.Tasks.Task DeleteAsync(int id);

        // Quản lý Skills của Draft
        System.Threading.Tasks.Task AddSkillAsync(TaskDraftSkill skill);
        Task<List<TaskDraftSkill>> GetSkillsByDraftIdAsync(int draftId);

        // Update trạng thái cả cây (Parent + Subtasks)
        System.Threading.Tasks.Task UpdateStatusTreeAsync(int rootDraftId, string status, int userId, string? reason = null);

    }
}