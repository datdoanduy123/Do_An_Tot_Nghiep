using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace DocTask.Data.Repositories
{
    public class TaskDraftRepository : ITaskDraftRepository
    {
        private readonly ApplicationDbContext _context;


        public TaskDraftRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<TaskDraft> CreateAsync(TaskDraft draft)
        {
            _context.TaskDrafts.Add(draft);
            await _context.SaveChangesAsync();
            return draft;
        }
        public async Task<TaskDraft?> GetByIdAsync(int id)
        {
            return await _context.TaskDrafts.FirstOrDefaultAsync(d => d.DraftId == id);
        }
        public async Task UpdateAsync(TaskDraft draft)
        {
            _context.TaskDrafts.Update(draft);
            await _context.SaveChangesAsync();
        }
        public async Task DeleteAsync(int id)
        {
            var draft = await GetByIdAsync(id);
            if (draft != null)
            {
                _context.TaskDrafts.Remove(draft);
                await _context.SaveChangesAsync();
            }
        }

        public async Task<List<TaskDraft>> GetByFileIdAsync(int fileId)
        {
            return await _context.TaskDrafts
                .Where(d => d.FileId == fileId && d.ParentDraftId == null)
                .Include(d => d.InverseParentDraft) // Include level 1 children (optional viewing)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();
        }

        public async Task<TaskDraft?> GetByIdWithChildrenAsync(int id)
        {
            var root = await _context.TaskDrafts
                .Include(d => d.DraftSkills).ThenInclude(ds => ds.Skill)
                .Include(d => d.InverseParentDraft).ThenInclude(sub => sub.DraftSkills).ThenInclude(ss => ss.Skill)
                .FirstOrDefaultAsync(d => d.DraftId == id);

            return root;
        }

        public async Task AddSkillAsync(TaskDraftSkill skill)
        {
            _context.TaskDraftSkills.Add(skill);
            await _context.SaveChangesAsync();
        }

        public async Task<List<TaskDraftSkill>> GetSkillsByDraftIdAsync(int draftId)
        {
            return await _context.TaskDraftSkills
                .Include(s => s.Skill)
                .Where(s => s.DraftId == draftId)
                .ToListAsync();
        }

        public async Task UpdateStatusTreeAsync(int rootDraftId, string status, int userId, string? reason = null)
        {
            // Update Root
            var root = await GetByIdWithChildrenAsync(rootDraftId);
            if (root == null) return;
            UpdateSingleDraftStatus(root, status, userId, reason);
            // Update Children
            if (root.InverseParentDraft != null)
            {
                foreach (var child in root.InverseParentDraft)
                {
                    UpdateSingleDraftStatus(child, status, userId, reason);
                }
            }
            await _context.SaveChangesAsync();
        }

        private void UpdateSingleDraftStatus(TaskDraft draft, string status, int userId, string? reason)
        {
            draft.Status = status;
            draft.ReviewedBy = userId;
            draft.ReviewedAt = DateTime.UtcNow;
            if (!string.IsNullOrEmpty(reason))
            {
                draft.ReviewNotes = reason;
            }
        }
    }
}