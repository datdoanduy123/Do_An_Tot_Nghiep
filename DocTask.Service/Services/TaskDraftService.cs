using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DocTask.Core.Dtos.Draft;
using DocTask.Core.Dtos.Gemini;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;

namespace DocTask.Service.Services
{
    /// <summary>
    /// Service quản lý vòng đời Draft:
    /// Tạo từ AI → Review (xem/sửa) → Approve (tạo Task thật) / Reject
    /// </summary>
    public class TaskDraftService : ITaskDraftService
    {
        private readonly ITaskDraftRepository _draftRepository;
        private readonly ITaskService _taskService;
        private readonly DocTask.Data.ApplicationDbContext _dbContext;

        public TaskDraftService(
            ITaskDraftRepository draftRepository,
            ITaskService taskService,
            DocTask.Data.ApplicationDbContext dbContext)
        {
            _draftRepository = draftRepository;
            _taskService = taskService;
            _dbContext = dbContext;
        }

        // ================================================================
        //  TẠO DRAFT TỪ AI
        // ================================================================

        /// <summary>
        /// Tạo Draft tree từ dữ liệu AI (root + subtasks + skills).
        /// Truncate title/desc để tránh lỗi DB.
        /// </summary>
        public async Task<int> CreateDraftFromGeminiAsync(object geminiTaskData, int fileId, int userId)
        {
            try
            {
                // 1. Parsing Data từ AI
                GeminiDto.GeminiTaskDto aiTask;
                if (geminiTaskData is JsonElement jsonElement)
                {
                    aiTask = JsonSerializer.Deserialize<GeminiDto.GeminiTaskDto>(jsonElement.GetRawText())
                             ?? throw new Exception("Failed to deserialize AI task data");
                }
                else if (geminiTaskData is GeminiDto.GeminiTaskDto dto)
                {
                    aiTask = dto;
                }
                else
                {
                    var json = JsonSerializer.Serialize(geminiTaskData);
                    aiTask = JsonSerializer.Deserialize<GeminiDto.GeminiTaskDto>(json)
                             ?? throw new Exception("Invalid AI task data format");
                }

                // 2. Tạo Root Draft (truncate title/desc để tránh lỗi DB)
                var safeTitle = TruncateString(aiTask.Title, 100);
                var safeDesc = TruncateString(aiTask.Description, 500);

                var rootDraft = new TaskDraft
                {
                    FileId = fileId,
                    CreatedBy = userId,
                    Title = safeTitle ?? "Dự án mới",
                    Description = safeDesc,
                    StartDate = aiTask.StartDate,
                    EndDate = aiTask.EndDate,
                    EstimatedHours = aiTask.EstimatedHours,
                    RawAIResponse = JsonSerializer.Serialize(aiTask),
                    Status = "Pending",
                    ParentDraftId = null,
                    CreatedAt = DateTime.UtcNow
                };

                await _draftRepository.CreateAsync(rootDraft);
                Console.WriteLine($"✅ Created root draft with ID: {rootDraft.DraftId}");

                // 3. Lưu Skill cho Root Task
                try
                {
                    await SaveDraftSkillsAsync(rootDraft.DraftId, aiTask.RequiredSkills);
                    Console.WriteLine($"✅ Saved {aiTask.RequiredSkills?.Count ?? 0} skills for root draft");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error saving root draft skills: {ex.Message}");
                }

                // 4. Tạo Epic/Task Drafts (Recursive 3 level)
                // ⭐ Ưu tiên Epics (schema mới 3 level), fallback sang Subtasks (schema cũ 2 level)
                List<GeminiDto.GeminiSubtaskDto>? subtasksToCreate = null;

                if (aiTask.Epics != null && aiTask.Epics.Any())
                {
                    // Map Epic (Level 2) → SubtaskDto có Subtasks = Tasks (Level 3)
                    subtasksToCreate = aiTask.Epics.Select(epic => new GeminiDto.GeminiSubtaskDto
                    {
                        Title          = epic.Title,
                        Description    = epic.Description,
                        StartDate      = epic.StartDate,
                        DueDate        = epic.DueDate,
                        EstimatedHours = epic.EstimatedHours,
                        Priority       = "Medium",
                        RequiredSkills = epic.RequiredSkills,
                        // Tasks (Level 3) → Subtasks lồng bên trong Epic
                        Subtasks = epic.Tasks?.Select(t => new GeminiDto.GeminiSubtaskDto
                        {
                            Title          = t.Title,
                            Description    = t.Description,
                            StartDate      = t.StartDate,
                            DueDate        = t.DueDate,
                            EstimatedHours = t.EstimatedHours,
                            Priority       = t.Priority,
                            RequiredSkills = t.RequiredSkills,
                            Subtasks       = new List<GeminiDto.GeminiSubtaskDto>()
                        }).ToList() ?? new List<GeminiDto.GeminiSubtaskDto>()
                    }).ToList();

                    Console.WriteLine($"✅ [TaskDraftService] Mapping {aiTask.Epics.Count} Epics → SubtaskDtos (3-level)");
                }

                if (subtasksToCreate != null && subtasksToCreate.Any())
                {
                    await CreateSubTaskDrafts(rootDraft.DraftId, subtasksToCreate, fileId, userId);
                }

                Console.WriteLine($"🎉 Draft creation completed! draftId: {rootDraft.DraftId}");
                return rootDraft.DraftId;

            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌❌❌ CRITICAL ERROR in CreateDraftFromGeminiAsync: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return -1;
            }
        }

        // ================================================================
        //  XEM / REVIEW DRAFT
        // ================================================================

        /// <summary>
        /// Lấy danh sách root drafts theo fileId
        /// </summary>
        public async Task<List<TaskDraft>> GetDraftsByFileIdAsync(int fileId)
        {
            return await _draftRepository.GetByFileIdAsync(fileId);
        }

        /// <summary>
        /// Lấy raw draft tree (model gốc) — dùng nội bộ
        /// </summary>
        public async Task<TaskDraft?> GetDraftTreeAsync(int draftId)
        {
            return await _draftRepository.GetByIdWithChildrenAsync(draftId);
        }

        /// <summary>
        /// Lấy draft tree dưới dạng DTO flatten — dùng cho API response.
        /// Map model → DraftTreeDto với skills + subtasks rõ ràng.
        /// </summary>
        public async Task<DraftTreeDto?> GetDraftTreeDtoAsync(int draftId)
        {
            var draft = await _draftRepository.GetByIdWithChildrenAsync(draftId);
            if (draft == null) return null;

            return MapToDraftTreeDto(draft);
        }

        // ================================================================
        //  EDIT DRAFT (trước khi Approve)
        // ================================================================

        /// <summary>
        /// Cập nhật draft fields. PATCH semantics: null = giữ nguyên.
        /// Chỉ cho phép khi Status = "Pending".
        /// </summary>
        public async Task<DraftTreeDto> UpdateDraftAsync(int draftId, UpdateDraftDto dto, int userId)
        {
            var draft = await _draftRepository.GetByIdAsync(draftId);
            if (draft == null)
                throw new KeyNotFoundException("Draft không tồn tại.");

            // Bảo vệ: chỉ sửa khi Pending
            if (draft.Status != "Pending")
                throw new BadRequestException($"Không thể sửa draft đã {draft.Status}.");

            // PATCH: chỉ update field có giá trị
            if (dto.Title != null)
                draft.Title = TruncateString(dto.Title, 100) ?? draft.Title;

            if (dto.Description != null)
                draft.Description = TruncateString(dto.Description, 500);

            if (dto.StartDate.HasValue)
                draft.StartDate = dto.StartDate.Value;

            if (dto.EndDate.HasValue)
                draft.EndDate = dto.EndDate.Value;

            if (dto.EstimatedHours.HasValue)
                draft.EstimatedHours = dto.EstimatedHours.Value;

            // Validate: StartDate <= EndDate
            if (draft.StartDate.HasValue && draft.EndDate.HasValue
                && draft.StartDate.Value > draft.EndDate.Value)
            {
                throw new BadRequestException("StartDate phải <= EndDate.");
            }

            await _draftRepository.UpdateAsync(draft);

            // Trả về DTO đầy đủ
            return (await GetDraftTreeDtoAsync(draftId))!;
        }

        /// <summary>
        /// Xóa subtask draft.
        /// Bảo vệ 1: Không cho xóa root (ParentDraftId == null).
        /// Bảo vệ 2: Không cho xóa nếu Status != "Pending".
        /// </summary>
        public async Task DeleteSubDraftAsync(int subDraftId, int userId)
        {
            var draft = await _draftRepository.GetByIdAsync(subDraftId);
            if (draft == null)
                throw new KeyNotFoundException("SubDraft không tồn tại.");

            // Bảo vệ: không cho xóa root
            if (draft.ParentDraftId == null)
                throw new BadRequestException("Không thể xóa root draft. Chỉ có thể xóa subtask.");

            // Bảo vệ: chỉ xóa khi Pending
            if (draft.Status != "Pending")
                throw new BadRequestException($"Không thể xóa draft đã {draft.Status}.");

            await _draftRepository.DeleteAsync(subDraftId);
            Console.WriteLine($"🗑️ Deleted sub-draft {subDraftId} by user {userId}");
        }

        // ================================================================
        //  APPROVE DRAFT → TẠO TASK THẬT
        // ================================================================

        /// <summary>
        /// Approve draft → validate → timeline clamp → tạo Task thật.
        /// 1. Validate: title không trống, StartDate <= EndDate, EstimatedHours > 0
        /// 2. Timeline clamp: subtask date nằm trong parent range
        /// 3. Tạo Task root + child tasks
        /// 4. Copy skills từ Draft → TaskSkillRequirement
        /// 5. Set IsAIGenerated = true
        /// 6. Update draft status → "Approved"
        /// </summary>
        public async Task<TaskDto> ApproveDraftAsync(int draftId, int userId)
        {
            // 1. Get Full Draft Tree
            var rootDraft = await _draftRepository.GetByIdWithChildrenAsync(draftId);
            if (rootDraft == null)
                throw new KeyNotFoundException("Draft không tồn tại.");

            if (rootDraft.Status != "Pending")
                throw new BadRequestException($"Draft đã được {rootDraft.Status}, không thể approve lại.");

            // 2. Validate root draft
            ValidateDraft(rootDraft);

            // 3. Tạo Task root — set IsAIGenerated
            var parentEntity = new DocTask.Core.Models.Task
            {
                Title = rootDraft.Title,
                Description = rootDraft.Description,
                AssignerId = userId,
                StartDate = rootDraft.StartDate,
                DueDate = rootDraft.EndDate,
                EstimatedHours = rootDraft.EstimatedHours,
                IsAIGenerated = true,
                CreatedAt = DateTime.UtcNow,
                Status = "NotStarted"
            };

            _dbContext.Tasks.Add(parentEntity);
            await _dbContext.SaveChangesAsync();
            Console.WriteLine($"✅ Created root task {parentEntity.TaskId} from draft {draftId}");

            // 4. Copy skills cho root task
            await CopySkillsFromDraftToTask(rootDraft.DraftId, parentEntity.TaskId);

            // 5. Tạo child tasks (với timeline clamp)
            if (rootDraft.InverseParentDraft != null)
            {
                foreach (var childDraft in rootDraft.InverseParentDraft)
                {
                    // Timeline clamp: subtask phải nằm trong parent range
                    ClampSubtaskTimeline(childDraft, rootDraft);

                    var childEntity = new DocTask.Core.Models.Task
                    {
                        Title = childDraft.Title,
                        Description = childDraft.Description,
                        AssignerId = userId,
                        ParentTaskId = parentEntity.TaskId, // Link parent TRƯỚC khi save
                        StartDate = childDraft.StartDate,
                        DueDate = childDraft.EndDate,
                        EstimatedHours = childDraft.EstimatedHours,
                        IsAIGenerated = true,
                        CreatedAt = DateTime.UtcNow,
                        Status = "NotStarted"
                    };

                    _dbContext.Tasks.Add(childEntity);
                    await _dbContext.SaveChangesAsync();

                    // Copy skills cho child
                    await CopySkillsFromDraftToTask(childDraft.DraftId, childEntity.TaskId);

                    Console.WriteLine($"  ✅ Created child task {childEntity.TaskId} (parent: {parentEntity.TaskId})");

                    // 5.1 Create grandchild tasks (Work Packages)
                    if (childDraft.InverseParentDraft != null && childDraft.InverseParentDraft.Any())
                    {
                        foreach (var grandChildDraft in childDraft.InverseParentDraft)
                        {
                             // Clamp timeline: WorkPackage must be within Module
                            ClampSubtaskTimeline(grandChildDraft, childDraft);

                            var grandChildEntity = new DocTask.Core.Models.Task
                            {
                                Title = grandChildDraft.Title,
                                Description = grandChildDraft.Description,
                                AssignerId = userId,
                                ParentTaskId = childEntity.TaskId, // Link to Module
                                StartDate = grandChildDraft.StartDate,
                                DueDate = grandChildDraft.EndDate,
                                EstimatedHours = grandChildDraft.EstimatedHours,
                                IsAIGenerated = true,
                                CreatedAt = DateTime.UtcNow,
                                Status = "NotStarted"
                            };

                            _dbContext.Tasks.Add(grandChildEntity);
                            await _dbContext.SaveChangesAsync();

                            // Copy skills for grandchild
                            await CopySkillsFromDraftToTask(grandChildDraft.DraftId, grandChildEntity.TaskId);
                            
                             Console.WriteLine($"    ✅ Created Work Package {grandChildEntity.TaskId} (parent: {childEntity.TaskId})");
                        }
                    }
                }
            }

            // 6. Update draft status → Approved
            await _draftRepository.UpdateStatusTreeAsync(draftId, "Approved", userId);
            rootDraft.CreatedTaskId = parentEntity.TaskId;
            await _draftRepository.UpdateAsync(rootDraft);

            Console.WriteLine($"🎉 Draft {draftId} approved → Task {parentEntity.TaskId}");

            // 7. Trả về TaskDto
            var taskDto = new TaskDto
            {
                TaskId = parentEntity.TaskId,
                Title = parentEntity.Title,
                Description = parentEntity.Description,
                StartDate = parentEntity.StartDate,
                DueDate = parentEntity.DueDate,
                Status = parentEntity.Status,
                ParentTaskId = parentEntity.ParentTaskId
            };
            return taskDto;
        }

        // ================================================================
        //  REJECT DRAFT
        // ================================================================

        /// <summary>
        /// Reject draft — lưu lý do, đánh status cả cây = "Rejected"
        /// </summary>
        public async Task RejectDraftAsync(int draftId, int userId, string? reason = null)
        {
            var draft = await _draftRepository.GetByIdAsync(draftId);
            if (draft == null)
                throw new KeyNotFoundException("Draft không tồn tại.");

            if (draft.Status != "Pending")
                throw new BadRequestException($"Draft đã được {draft.Status}, không thể reject.");

            await _draftRepository.UpdateStatusTreeAsync(draftId, "Rejected", userId, reason);
            Console.WriteLine($"❌ Draft {draftId} rejected by user {userId}. Reason: {reason ?? "N/A"}");
        }

        // ================================================================
        //  PRIVATE HELPERS
        // ================================================================

        /// <summary>
        /// Validate draft trước khi approve.
        /// Check: title có nội dung, date hợp lệ, EstimatedHours hợp lý.
        /// </summary>
        private void ValidateDraft(TaskDraft draft)
        {
            if (string.IsNullOrWhiteSpace(draft.Title))
                throw new BadRequestException("Title không được để trống.");

            if (draft.StartDate.HasValue && draft.EndDate.HasValue
                && draft.StartDate.Value > draft.EndDate.Value)
            {
                throw new BadRequestException("StartDate phải nhỏ hơn hoặc bằng EndDate.");
            }

            if (draft.EstimatedHours.HasValue && draft.EstimatedHours.Value <= 0)
            {
                throw new BadRequestException("EstimatedHours phải lớn hơn 0.");
            }
        }

        /// <summary>
        /// Timeline clamp: đảm bảo subtask nằm trong timeline của parent.
        /// - sub.Start < parent.Start → sub.Start = parent.Start
        /// - sub.End > parent.End   → sub.End = parent.End
        /// - sub.End < sub.Start    → sub.End = sub.Start + 3 ngày
        /// </summary>
        private void ClampSubtaskTimeline(TaskDraft child, TaskDraft parent)
        {
            if (!parent.StartDate.HasValue || !parent.EndDate.HasValue) return;

            // Clamp start
            if (child.StartDate.HasValue && child.StartDate.Value < parent.StartDate.Value)
                child.StartDate = parent.StartDate.Value;

            // Clamp end
            if (child.EndDate.HasValue && child.EndDate.Value > parent.EndDate.Value)
                child.EndDate = parent.EndDate.Value;

            // Fix: end < start → end = start + 3 ngày (hoặc parent.End nếu gần)
            if (child.StartDate.HasValue && child.EndDate.HasValue
                && child.EndDate.Value < child.StartDate.Value)
            {
                var fallbackEnd = child.StartDate.Value.AddDays(3);
                child.EndDate = fallbackEnd <= parent.EndDate.Value
                    ? fallbackEnd
                    : parent.EndDate.Value;
            }

            // Nếu subtask không có date → dùng parent date
            if (!child.StartDate.HasValue)
                child.StartDate = parent.StartDate.Value;
            if (!child.EndDate.HasValue)
                child.EndDate = parent.EndDate.Value;
        }

        /// <summary>
        /// Copy skills từ TaskDraftSkill → TaskSkillRequirement
        /// </summary>
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
            await _dbContext.SaveChangesAsync();
        }

        /// <summary>
        /// Lưu skills cho draft (tìm hoặc tạo Skill master data)
        /// </summary>
        private async Task SaveDraftSkillsAsync(int draftId, List<GeminiDto.GeminiSkillRequirementDTO>? skillDtos)
        {
            if (skillDtos == null || !skillDtos.Any()) return;

            foreach (var s in skillDtos)
            {
                // Tìm hoặc tạo Skill trong Master Data
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
                        Description = "Auto-created from AI Draft",
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

                try { await _draftRepository.AddSkillAsync(draftSkill); }
                catch { /* Ignore duplicates */ }
            }
        }

        /// <summary>
        /// Map TaskDraft model → DraftTreeDto (flatten skills + subtasks)
        /// </summary>
        private DraftTreeDto MapToDraftTreeDto(TaskDraft draft)
        {
            return new DraftTreeDto
            {
                DraftId = draft.DraftId,
                FileId = draft.FileId,
                CreatedBy = draft.CreatedBy,
                Title = draft.Title,
                Description = draft.Description,
                Status = draft.Status,
                StartDate = draft.StartDate,
                EndDate = draft.EndDate,
                EstimatedHours = draft.EstimatedHours,
                CreatedTaskId = draft.CreatedTaskId,
                ReviewNotes = draft.ReviewNotes,
                CreatedAt = draft.CreatedAt,
                ReviewedAt = draft.ReviewedAt,
                // Map skills
                Skills = draft.DraftSkills?.Select(ds => new DraftSkillDto
                {
                    SkillId = ds.SkillId,
                    SkillName = ds.Skill?.SkillName ?? "Unknown",
                    RequiredLevel = ds.RequiredLevel,
                    Importance = ds.Importance
                }).ToList() ?? new List<DraftSkillDto>(),
                // Map subtasks
                // Map subtasks
                Subtasks = draft.InverseParentDraft?.Select(MapToDraftSubtaskDto).ToList() ?? new List<DraftSubtaskDto>()
            };
        }

        /// <summary>
        /// Tạo subtasks đệ quy
        /// </summary>
        private async Task CreateSubTaskDrafts(int parentDraftId, List<GeminiDto.GeminiSubtaskDto> subtasks, int fileId, int userId)
        {
            if (subtasks == null || !subtasks.Any()) return;

            Console.WriteLine($"📝 Creating {subtasks.Count} subtasks for parent {parentDraftId}...");
            foreach (var sub in subtasks)
            {
                try
                {
                    var subDraft = new TaskDraft
                    {
                        FileId = fileId,
                        CreatedBy = userId,
                        Title = TruncateString(sub.Title, 100) ?? "Subtask",
                        Description = TruncateString(sub.Description, 500),
                        StartDate = sub.StartDate,
                        EndDate = sub.DueDate,
                        EstimatedHours = sub.EstimatedHours,
                        ParentDraftId = parentDraftId,
                        Status = "Pending",
                        CreatedAt = DateTime.UtcNow
                    };
                    await _draftRepository.CreateAsync(subDraft);

                    // Lưu skill cho subtask
                    try { await SaveDraftSkillsAsync(subDraft.DraftId, sub.RequiredSkills); }
                    catch (Exception skillEx) { Console.WriteLine($"⚠️ Skill save error: {skillEx.Message}"); }

                    // Recursive call cho sub-subtasks
                    if (sub.Subtasks != null && sub.Subtasks.Any())
                    {
                        await CreateSubTaskDrafts(subDraft.DraftId, sub.Subtasks, fileId, userId);
                    }
                }
                catch (Exception subEx)
                {
                    Console.WriteLine($"❌ Error creating subtask: {subEx.Message}");
                }
            }
        }

        private DraftSubtaskDto MapToDraftSubtaskDto(TaskDraft sub)
        {
            return new DraftSubtaskDto
            {
                DraftId = sub.DraftId,
                Title = sub.Title,
                Description = sub.Description,
                StartDate = sub.StartDate,
                EndDate = sub.EndDate,
                EstimatedHours = sub.EstimatedHours,
                Status = sub.Status, // Pending
                Skills = sub.DraftSkills?.Select(ss => new DraftSkillDto
                {
                    SkillId = ss.SkillId,
                    SkillName = ss.Skill?.SkillName ?? "Unknown",
                    RequiredLevel = ss.RequiredLevel,
                    Importance = ss.Importance
                }).ToList() ?? new List<DraftSkillDto>(),
                Subtasks = sub.InverseParentDraft?.Select(MapToDraftSubtaskDto).ToList() ?? new List<DraftSubtaskDto>()
            };
        }

        /// <summary>
        /// Truncate string an toàn (null-safe)
        /// </summary>
        private static string? TruncateString(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
        }
    }
}