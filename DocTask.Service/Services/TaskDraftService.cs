using System.Text.Json;
using DocTask.Core.Dtos.Gemini;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace DocTask.Service.Services
{
    public class TaskDraftService : ITaskDraftService
    {
        private readonly ITaskDraftRepository _draftRepository;
        private readonly ITaskService _taskService;
        private readonly DocTask.Data.ApplicationDbContext _dbContext; // Inject DbContext để xử lý Skills nhanh
        public TaskDraftService(
            ITaskDraftRepository draftRepository,
            ITaskService taskService,
            DocTask.Data.ApplicationDbContext dbContext)
        {
            _draftRepository = draftRepository;
            _taskService = taskService;
            _dbContext = dbContext;
        }

        public async Task<int> CreateDraftFromGeminiAsync(object geminiTaskData, int fileId, int userId)
        {
            // 1. Parsing Data từ AI
            GeminiDto.GeminiTaskDto aiTask;
            if (geminiTaskData is JsonElement jsonElement)
            {
                aiTask = JsonSerializer.Deserialize<GeminiDto.GeminiTaskDto>(jsonElement.GetRawText())
                         ?? throw new System.Exception("Failed to deserialize AI task data");
            }
            else if (geminiTaskData is GeminiDto.GeminiTaskDto dto)
            {
                aiTask = dto;
            }
            else
            {
                var json = JsonSerializer.Serialize(geminiTaskData);
                aiTask = JsonSerializer.Deserialize<GeminiDto.GeminiTaskDto>(json)
                         ?? throw new System.Exception("Invalid AI task data format");
            }
            // 2. Tạo Root Draft
            var rootDraft = new TaskDraft
            {
                FileId = fileId,
                CreatedBy = userId,
                Title = aiTask.Title,
                Description = aiTask.Description,
                StartDate = aiTask.StartDate,
                EndDate = aiTask.EndDate,
                EstimatedHours = aiTask.EstimatedHours,
                RawAIResponse = JsonSerializer.Serialize(aiTask),
                Status = "Pending",
                ParentDraftId = null
            };
            await _draftRepository.CreateAsync(rootDraft);

            // 3. Lưu Skill cho Root Task
            await SaveDraftSkillsAsync(rootDraft.DraftId, aiTask.RequiredSkills);
            // 4. Tạo Subtasks (Recursive 1 cấp)
            if (aiTask.Subtasks != null)
            {
                foreach (var subDt in aiTask.Subtasks)
                {
                    var subDraft = new TaskDraft
                    {
                        FileId = fileId,
                        CreatedBy = userId,
                        Title = subDt.Title,
                        Description = subDt.Description,
                        StartDate = subDt.StartDate,
                        EndDate = subDt.DueDate,
                        EstimatedHours = subDt.EstimatedHours,
                        ParentDraftId = rootDraft.DraftId,
                        Status = "Pending"
                    };
                    await _draftRepository.CreateAsync(subDraft);

                    // Lưu Skill cho Subtask
                    await SaveDraftSkillsAsync(subDraft.DraftId, subDt.RequiredSkills);
                }
            }
            return rootDraft.DraftId;
        }
        private async Task SaveDraftSkillsAsync(int draftId, List<GeminiDto.GeminiSkillRequirementDTO> skillDtos)
        {
            if (skillDtos == null || !skillDtos.Any()) return;
            foreach (var s in skillDtos)
            {
                // Tìm hoặc tạo Skill trong Master Data (Fuzzy match)
                var skillNameLower = s.SkillName.Trim().ToLower();
                var existingSkill = await _dbContext.Skills
                    .FirstOrDefaultAsync(sk => sk.SkillName.ToLower() == skillNameLower);
                int skillId;
                if (existingSkill != null)
                {
                    skillId = existingSkill.SkillId;
                }
                else
                {
                    // Auto-create new skill
                    var newSkill = new Skill
                    {
                        SkillName = s.SkillName.Trim(),
                        Description = $"Auto-created from AI Draft",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };
                    _dbContext.Skills.Add(newSkill);
                    await _dbContext.SaveChangesAsync();
                    skillId = newSkill.SkillId;
                }
                // Add to TaskDraftSkill
                var draftSkill = new TaskDraftSkill
                {
                    DraftId = draftId,
                    SkillId = skillId,
                    RequiredLevel = s.RequiredLevel,
                    Importance = s.Importance,
                    IsAIExtracted = true
                };

                try { await _draftRepository.AddSkillAsync(draftSkill); } catch { } // Ignore duplicates
            }
        }
        public async Task<List<TaskDraft>> GetDraftsByFileIdAsync(int fileId)
        {
            return await _draftRepository.GetByFileIdAsync(fileId);
        }
        public async Task<TaskDraft?> GetDraftTreeAsync(int draftId)
        {
            return await _draftRepository.GetByIdWithChildrenAsync(draftId);
        }
        public async Task<TaskDto> ApproveDraftAsync(int draftId, int userId)
        {
            // 1. Get Full Draft Tree
            var rootDraft = await _draftRepository.GetByIdWithChildrenAsync(draftId);
            if (rootDraft == null) throw new KeyNotFoundException("Draft not found");
            // 2. Map to CreateTaskDto for Root
            var createTaskDto = new CreateTaskDto
            {
                Title = rootDraft.Title,
                Description = rootDraft.Description,
                StartDate = rootDraft.StartDate,
                DueDate = rootDraft.EndDate,
                // AssigneeId logic ở đây...
            };
            var parentTaskDto = await _taskService.CreateTaskAsync(createTaskDto, userId);

            // 3. Copy Skills
            await CopySkillsFromDraftToTask(rootDraft.DraftId, parentTaskDto.TaskId);

            // 4. Process Children
            if (rootDraft.InverseParentDraft != null)
            {
                foreach (var childDraft in rootDraft.InverseParentDraft)
                {
                    var createSubDto = new CreateTaskDto
                    {
                        Title = childDraft.Title,
                        Description = childDraft.Description,
                        StartDate = childDraft.StartDate,
                        DueDate = childDraft.EndDate,
                    };

                    var childTaskDto = await _taskService.CreateTaskAsync(createSubDto, userId);

                    // Link to Parent (Manual update via Context for now)
                    var childEntity = await _dbContext.Tasks.FindAsync(childTaskDto.TaskId);
                    if (childEntity != null)
                    {
                        childEntity.ParentTaskId = parentTaskDto.TaskId;
                        await CopySkillsFromDraftToTask(childDraft.DraftId, childTaskDto.TaskId);
                    }
                }
                await _dbContext.SaveChangesAsync();
            }
            // 5. Update Status
            await _draftRepository.UpdateStatusTreeAsync(draftId, "Approved", userId);
            rootDraft.CreatedTaskId = parentTaskDto.TaskId;
            await _draftRepository.UpdateAsync(rootDraft);

            return parentTaskDto;
        }
        private async Task CopySkillsFromDraftToTask(int draftId, int taskId)
        {
            var draftSkills = await _draftRepository.GetSkillsByDraftIdAsync(draftId);
            foreach (var ds in draftSkills)
            {
                var taskSkill = new TaskSkillRequirement
                {
                    TaskId = taskId,
                    SkillId = ds.SkillId,
                    RequiredLevel = ds.RequiredLevel,
                    Importance = ds.Importance,
                    IsAutoExtracted = ds.IsAIExtracted,
                    CreatedAt = DateTime.UtcNow
                };
                _dbContext.TaskSkillRequirements.Add(taskSkill);
            }
        }
        public async Task RejectDraftAsync(int draftId, int userId, string? reason = null)
        {
            await _draftRepository.UpdateStatusTreeAsync(draftId, "Rejected", userId, reason);
        }
    }
}