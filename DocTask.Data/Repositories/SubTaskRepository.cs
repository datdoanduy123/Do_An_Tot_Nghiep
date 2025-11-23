using System.Text;
using DocTask.Core.Dtos.SubTasks;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using TaskEntity = DocTask.Core.Models.Task;

namespace DocTask.Data.Repositories
{
    public class SubTaskRepository : ISubTaskRepository
    {
        private readonly ApplicationDbContext _context;

        public SubTaskRepository(ApplicationDbContext context)
        {
            _context = context;
        }


        public async Task<TaskEntity?> GetByIdAsync(int subTaskId)
        {
            return await _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Frequency!)
                    .ThenInclude(f => f.FrequencyDetails)
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(t => t.TaskId == subTaskId && t.IsDeleted == false);
        }
        
        public async Task<TaskEntity?> GetByIdWithUsersAndUnitsAsync(int subTaskId)
        {
            return await _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Frequency!)
                .ThenInclude(f => f.FrequencyDetails)
                .Include(t => t.Taskunitassignments)
                .ThenInclude(t => t.Unit)
                .ThenInclude(t => t.Org)
                .Include(t => t.Taskunitassignments)
                .ThenInclude(tua => tua.Unit)
                .ThenInclude(u => u.Unitusers)
                    .ThenInclude(uu => uu.User)
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(t => t.TaskId == subTaskId && t.IsDeleted == false);
        }
        
        public async Task<TaskEntity?> GetByIdWithFrequenciesAsync(int subTaskId)
        {
            return await _context.Tasks
                .Include(t => t.Frequency!)
                .ThenInclude(f => f.FrequencyDetails)
                .AsNoTracking()
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync(t => t.TaskId == subTaskId && t.IsDeleted == false);
        }
        
        public async Task<TaskEntity?> GetBySubIdAsync(int parentTaskId, int subTaskId)
        {
            return await _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Frequency!)
                    .ThenInclude(f => f.FrequencyDetails)
                .FirstOrDefaultAsync(t => t.TaskId == subTaskId && t.ParentTaskId == parentTaskId && t.IsDeleted == false);
        }

        public async Task<TaskEntity> CreateAsync(TaskEntity subTask)
        {
            subTask.CreatedAt = DateTime.UtcNow;
            _context.Tasks.Add(subTask);
            await _context.SaveChangesAsync();
            return subTask;
        }
        
        public async Task<TaskEntity?> CreateSubtaskAsync(int parentTaskId, CreateSubTaskRequest request, int userId)
        {
            TaskEntity createdSubTask;
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // create frequence
                var persistedFrequency = await _context.Frequencies.AddAsync(new Frequency
                {
                    FrequencyType = request.Frequency,
                    IntervalValue = request.IntervalValue,
                });
                
                await _context.SaveChangesAsync();

                if (request.Frequency != "daily" && request.Days != null && request.Days.Any())
                {
                    foreach (var day in request.Days)
                    {
                        var frequencyDetail = new FrequencyDetail
                        {
                            FrequencyId = persistedFrequency.Entity.FrequencyId,
                            DayOfMonth = request.Frequency == "monthly" ? day : null,
                            DayOfWeek = request.Frequency == "weekly" ? day : null,
                        };
                        await _context.FrequencyDetails.AddAsync(frequencyDetail);
                        await _context.SaveChangesAsync();
                    }
                }

                //Tạo Subtask
                var subTaskEntity = new TaskEntity
                {
                    Title = request.Title,
                    Description = request.Description,
                    AssignerId = null,
                    Status = "pending", // Default value
                    Priority = "medium", // Default value
                    StartDate = request.StartDate, // DateTime? to DateTime?
                    DueDate = request.DueDate,     // DateTime? to DateTime?
                    Percentagecomplete = 0, // Default value
                    CreatedAt = DateTime.UtcNow
                };
                
                subTaskEntity.AssignerId = userId;
                subTaskEntity.ParentTaskId = parentTaskId;
                subTaskEntity.FrequencyId = persistedFrequency.Entity.FrequencyId;

                if (request.AssignedUserIds.Count != 0)
                    foreach (var id in request.AssignedUserIds)
                    {
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
                        if (user != null)
                        {
                            subTaskEntity.Users.Add(user);
                        }
                    }
                
                await _context.Tasks.AddAsync(subTaskEntity);
                await _context.SaveChangesAsync();
                
                if (request.AssignedUserIds.Count == 0 && request.AssignedUnitIds.Count != 0)
                    foreach (var id in request.AssignedUnitIds!)
                    {
                        var unit = await _context.Units.FirstOrDefaultAsync(u => u.UnitId == id);
                        if (unit == null) continue;
                        var taskUnitAssignment = new Taskunitassignment
                        {
                            TaskId = subTaskEntity.TaskId,
                            UnitId = unit.UnitId
                        };
                        _context.Taskunitassignments.Add(taskUnitAssignment);
                        await _context.SaveChangesAsync();
                    }


                createdSubTask = subTaskEntity;
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new BadRequestException("Failed to execute transaction!");
            }
            
            return createdSubTask;
        }

        public async Task<TaskEntity?> UpdateSubtaskAsync(TaskEntity subtask, UpdateSubTaskRequest request)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                subtask.Title = request.Title ?? subtask.Title;
                subtask.Description = request.Description ?? subtask.Description;
                subtask.DueDate = request.DueDate ?? subtask.DueDate;
                subtask.StartDate = request.StartDate ?? subtask.StartDate;
                
                if (subtask.Frequency != null)
                {
                    subtask.Frequency.FrequencyType = request.Frequency ?? subtask.Frequency.FrequencyType;
                    subtask.Frequency.IntervalValue = request.IntervalValue ?? subtask.Frequency.IntervalValue;
                }
                
                _context.Tasks.Update(subtask);
                await _context.SaveChangesAsync();

                if (request.Frequency != null)
                    await _context.FrequencyDetails
                        .Where(f => f.FrequencyId == subtask.FrequencyId)
                        .ExecuteDeleteAsync();
                        
                if (request.Days != null && request.Frequency != "daily" && subtask.Frequency != null)
                    foreach (var day in request.Days)
                    {
                        var frequencyDetail = new FrequencyDetail
                        {
                            FrequencyId = subtask.Frequency.FrequencyId,
                            DayOfMonth = request.Frequency == "monthly" ? day : null,
                            DayOfWeek = request.Frequency == "weekly" ? day : null,
                        };
                        await _context.FrequencyDetails.AddAsync(frequencyDetail);
                        await _context.SaveChangesAsync();
                    }
             
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new BadRequestException("Failed to execute transaction!");
            }

            return subtask;
        }

        //Tu dong
        public async Task<TaskEntity?> UpdateSubtask(int subTaskId, TaskEntity subtask)
        {
            var existingSubtask = await _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Frequency!)
                    .ThenInclude(f => f.FrequencyDetails)
                .FirstOrDefaultAsync(t => t.TaskId == subtask.TaskId && t.IsDeleted == false);
            if (existingSubtask == null)
                return null;

            // Update basic properties
            existingSubtask.Title = subtask.Title;
            existingSubtask.Description = subtask.Description;
            existingSubtask.StartDate = subtask.StartDate;
            existingSubtask.DueDate = subtask.DueDate;

            // The Users collection is already updated in the service layer
            // Entity Framework will track the changes automatically

            await _context.SaveChangesAsync();
            return existingSubtask;
        }

        public async Task<bool> DeleteAsync(int subTaskId)
        {
            var subTask = await GetByIdAsync(subTaskId);
            if (subTask == null)
                return false;

            subTask.IsDeleted = true;
            //_context.Tasks.Update(subTask);
            _context.Entry(subTask).Property(x => x.IsDeleted).IsModified = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> ExistsAsync(int subTaskId)
        {
            return await _context.Tasks
                .AnyAsync(t => t.TaskId == subTaskId && t.ParentTaskId != null);
        }

        public async Task<List<TaskEntity>> GetAllByParentIdAsync(int parentTaskId)
        {
            return await _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Frequency!)
                    .ThenInclude(f => f.FrequencyDetails)
                .AsNoTracking()
                .Where(t => t.ParentTaskId == parentTaskId && t.IsDeleted == false)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task<PaginatedList<TaskEntity>> GetAllByParentIdPaginatedAsync(int parentTaskId, PageOptionsRequest pageOptions, string? key)
        {
            var query = _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Taskunitassignments)
                .ThenInclude(t => t.Unit)
                .ThenInclude(u => u.Org)
                .Include(t => t.Frequency!)
                .ThenInclude(f => f.FrequencyDetails)
                .AsNoTracking()
                .Where(t => t.ParentTaskId == parentTaskId && t.IsDeleted == false)
                .OrderByDescending(t => t.CreatedAt)
                .AsQueryable();

            if (key != null)
                query = query.Where(t => t.Title.StartsWith(key));
            
            return await query.ToPaginatedListAsync(pageOptions);
        }

        public async Task<List<TaskEntity>> GetByAssigneeIdAsync(int assigneeId)
        {
            return await _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Frequency!)
                    .ThenInclude(f => f.FrequencyDetails)
                .AsNoTracking()
                .Where(t => t.AssigneeId == assigneeId && t.ParentTaskId != null && t.IsDeleted == false)
                .OrderBy(t => t.DueDate)
                .ToListAsync();
        }

        public async Task<PaginatedList<TaskEntity>> GetByAssigneeIdPaginatedAsync(int assigneeId, PageOptionsRequest pageOptions)
        {
            var query = _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Frequency!)
                    .ThenInclude(f => f.FrequencyDetails)
                .AsNoTracking()
                .Where(t => t.AssigneeId == assigneeId && t.ParentTaskId != null && t.IsDeleted == false)
                .OrderBy(t => t.DueDate);

            return await query.ToPaginatedListAsync(pageOptions);
        }

        public async Task<List<TaskEntity>> GetByAssignedUserIdAsync(int userId)
        {
            return await _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Frequency!)
                    .ThenInclude(f => f.FrequencyDetails)
                .AsNoTracking()
                .Where(t => t.ParentTaskId != null && t.Users.Any(u => u.UserId == userId) && t.IsDeleted == false)
                .OrderBy(t => t.DueDate)
                .ToListAsync();
        }

        public async Task<PaginatedList<TaskEntity>> GetByAssignedUserIdPaginatedAsync(int userId, string? key, PageOptionsRequest pageOptions)
        {
           
            var taskIdsQuery = new StringBuilder(@"
                    select DISTINCT t1.taskId from dbo.task t1
                    left join dbo.taskassignees t2 on t2.TaskId = t1.taskId
                    left join dbo.taskunitassignment t3 on t3.TaskId = t1.taskId 
                    left join dbo.unituser u on u.unitId = t3.UnitId 
                    where t1.isDeleted = 0 and (t2.UserId = @p0 or u.userId = @p1 and u.[level] = 1)");
            
            if (key != null)
                taskIdsQuery.Append($" and t1.title like N'{key}%'");
                

            var taskIds = await _context.Database
                .SqlQueryRaw<int>(taskIdsQuery.ToString(), userId, userId)
                .ToListAsync();
            
            var taskQuery = _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Taskunitassignments)
                .ThenInclude(t => t.Unit)
                .ThenInclude(u => u.Org)
                .Include(t => t.Frequency!)
                .ThenInclude(f => f.FrequencyDetails)
                .AsNoTracking()
                .Where(t => taskIds.Contains(t.TaskId))
                .OrderByDescending(t => t.CreatedAt)
                .AsQueryable();
        
            return await taskQuery.ToPaginatedListAsync(pageOptions);
        }

        public async Task<List<TaskEntity>> GetByKeywordAsync(string keyword)
        {
            return await _context.Tasks
                .Include(t => t.Users)
                .Include(t => t.Frequency!)
                    .ThenInclude(f => f.FrequencyDetails)
                .AsNoTracking()
                .Where(t => t.ParentTaskId != null &&
                           (t.Title.Contains(keyword) ||
                            (t.Description != null && t.Description.Contains(keyword))) && t.IsDeleted == false)
                .OrderBy(t => t.CreatedAt)
                .ToListAsync();
        }

        public async Task AssignUsersToTaskAsync(int taskId, List<int> userIds)
        {
            var task = await _context.Tasks
                .Include(t => t.Users)
                .FirstOrDefaultAsync(t => t.TaskId == taskId && t.IsDeleted == false);

            if (task == null) return;

            // Clear all existing user assignments
            task.Users.Clear();

            // Add new user assignments
            if (userIds.Any())
            {
                var usersToAdd = await _context.Users
                    .Where(u => userIds.Contains(u.UserId))
                    .ToListAsync();

                foreach (var user in usersToAdd)
                {
                    task.Users.Add(user);
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<int>> GetAssignedUserIdsAsync(int taskId)
        {
            return await _context.Tasks
                .Where(t => t.TaskId == taskId && t.IsDeleted == false)
                .SelectMany(t => t.Users.Select(u => u.UserId))
                .ToListAsync();
        }

        public async Task RemoveUserFromTaskAsync(int taskId, int userId)
        {
            var task = await _context.Tasks
                .Include(t => t.Users)
                .FirstOrDefaultAsync(t => t.TaskId == taskId && t.IsDeleted == false);

            if (task == null) return;

            var userToRemove = task.Users.FirstOrDefault(u => u.UserId == userId);
            if (userToRemove != null)
            {
                task.Users.Remove(userToRemove);
                await _context.SaveChangesAsync();
            }
        }

 

      public async Task<bool> UpdateSubTaskStatus(int subTaskId, string status)
        {
            var statusList = new[] { "pending", "in_progress", "completed" };

            if (!statusList.Contains(status.ToLower())) return false;

            var task = await _context.Tasks
                .FirstOrDefaultAsync(t => t.TaskId == subTaskId);

            if (task == null) return false;

            

            task.Status = status;

            var subTasks = await _context.Tasks
                .Where(st => st.TaskId == subTaskId)
                .ToListAsync();

            task.Status = status.ToLower();
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<TaskEntity?> GetSubTaskWithParentAsync(int taskId)
        {
            return await _context.Tasks
                .FirstOrDefaultAsync(t => t.TaskId == taskId);
        }

        public async Task<List<UserResponse>> GetAssignedUsersAsync(int taskId)
        {
            return await _context.Tasks
                .Where(t => t.TaskId == taskId)
                .SelectMany(t => t.Users)
                .Select(u => new UserResponse
                {
                    UserId = u.UserId,
                    Username = u.Username,
                })
                .ToListAsync();
        }

        public async Task<List<Taskunitassignment>> GetUnitAssignmentsByTaskIdAsync(int subtaskId)
        {
            return await _context.Taskunitassignments
                .Include( tu => tu.Unit)
                    .ThenInclude( u => u.Org)
                .Where(tu => tu.TaskId == subtaskId)
                .ToListAsync();

        }

        public async Task<TaskEntity?> GetTaskUnitAssgment(int subtaskId)
        {
            return await _context.Tasks
                .Include(t => t.Frequency)
                    .ThenInclude(f => f.FrequencyDetails)
                .Include(t => t.Taskunitassignments)
                    .ThenInclude(ua => ua.Unit)
                .FirstOrDefaultAsync(t => t.TaskId == subtaskId);
        }
    }
}