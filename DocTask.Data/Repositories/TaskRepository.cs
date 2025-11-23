using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using Microsoft.EntityFrameworkCore;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Data.Repositories;

public class TaskRepository : ITaskRepository
{
  private readonly ApplicationDbContext _context;

  public TaskRepository(ApplicationDbContext context)
  {
    _context = context;
  }

  public async Task<PaginatedList<TaskModel>> GetAllAsync(PageOptionsRequest pageOptions, string? key, int userId)
  {
    var query = _context.Tasks
      .Where(t => t.ParentTaskId == null && t.AssignerId == userId && t.IsDeleted == false)
      .Include(t => t.Users)
      .Include(t => t.Assignee)
      .Include(t => t.Frequency)
      .ThenInclude(f => f.FrequencyDetails)
      .Include(t => t.Taskunitassignments)
      .OrderByDescending(t => t.CreatedAt).AsQueryable();
    if (key != null)
      query = query.Where(t => t.Title.StartsWith(key));

    return await query.ToPaginatedListAsync(pageOptions);
  }


  public async Task<TaskModel?> GetTaskByIdAsync(int taskId)
  {
    return await _context.Tasks
        .Include(t => t.Users)
        .Include(t => t.Assignee)
        .Include(t => t.Taskunitassignments)
        .Include(t => t.Frequency)
        .ThenInclude(f => f.FrequencyDetails)
        .FirstOrDefaultAsync(t => t.TaskId == taskId && t.IsDeleted == false);
  }

  public async Task<TaskModel?> CreateTaskAsync(TaskModel task)
  {
    _context.Tasks.Add(task);
    await _context.SaveChangesAsync();
    return await _context.Tasks
        .Include(t => t.Users)
        .Include(t => t.Taskunitassignments)
        .Include(t => t.Frequency)
            .ThenInclude(f => f.FrequencyDetails)
        .FirstOrDefaultAsync(t => t.TaskId == task.TaskId);
  }


  public async Task<TaskModel?> UpdateTaskAsync(int taskId, UpdateTaskDto taskDto)
  {
    var existingTask = await _context.Tasks
        .Include(t => t.Frequency)
        .ThenInclude(f => f.FrequencyDetails)
        .FirstOrDefaultAsync(t => t.TaskId == taskId && t.IsDeleted == false);

    if (existingTask == null)
      return null;

    // Cập nhật thông tin cơ bản
    existingTask.Title = taskDto.Title;
    existingTask.Description = taskDto.Description;
    existingTask.StartDate = taskDto.StartDate;
    existingTask.DueDate = taskDto.DueDate;

    if (!string.IsNullOrEmpty(taskDto.Frequency))
    {
      if (existingTask.Frequency == null)
      {
        // Nếu task chưa có frequency → tạo mới
        var newFrequency = new Frequency
        {
          FrequencyType = taskDto.Frequency,
          IntervalValue = taskDto.IntervalValue ?? 0,
          CreatedAt = DateTime.UtcNow
        };
        await _context.Frequencies.AddAsync(newFrequency);
        await _context.SaveChangesAsync(); // để có FrequencyId

        // Thêm frequency details
        if (taskDto.Days != null)
        {
          foreach (var day in taskDto.Days)
          {
            var fd = new FrequencyDetail
            {
              FrequencyId = newFrequency.FrequencyId,
              DayOfWeek = taskDto.Frequency.Equals("weekly", StringComparison.OrdinalIgnoreCase) ? day : null,
              DayOfMonth = taskDto.Frequency.Equals("monthly", StringComparison.OrdinalIgnoreCase) ? day : null
            };
            await _context.FrequencyDetails.AddAsync(fd);
          }
          await _context.SaveChangesAsync();
        }

        existingTask.FrequencyId = newFrequency.FrequencyId;
      }
      else
      {
        // Nếu đã có frequency → cập nhật qua service riêng
        await UpdateFrequencyAsync(
            existingTask.Frequency,
            taskDto.Frequency,
            taskDto.IntervalValue,
            taskDto.Days
        );
      }
    }

    await _context.SaveChangesAsync();

    // Trả lại bản ghi đầy đủ
    return await _context.Tasks
        .Include(t => t.Users)
        .Include(t => t.Taskunitassignments)
        .Include(t => t.Frequency)
            .ThenInclude(f => f.FrequencyDetails)
        .FirstOrDefaultAsync(t => t.TaskId == existingTask.TaskId);
  }


  public async Task<bool> DeleteAsync(TaskModel task)
  {
    using (var transaction = await _context.Database.BeginTransactionAsync())
    {
      try
      {
        var foundTask = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == task.TaskId && t.IsDeleted == false);
        if (foundTask == null)
          throw new NotFoundException($"Task with id {task.TaskId} not found.");
        await _context.Tasks
      .Where(e => e.ParentTaskId == foundTask.TaskId)
      .ExecuteUpdateAsync(setter => setter.SetProperty(e => e.IsDeleted, true));

        foundTask.IsDeleted = true;
        _context.Tasks.Update(foundTask);
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
      }
      catch (Exception)
      {
        await transaction.RollbackAsync();
        throw new BadRequestException("Error in execute command");
      }
    }

    return true;
  }

  public async Task<bool> CreateTaskUnitAssignmentAsync(int taskId, int unitId)
  {
    var taskUnitAssignment = new Core.Models.Taskunitassignment
    {
      TaskId = taskId,
      UnitId = unitId
    };

    _context.Taskunitassignments.Add(taskUnitAssignment);
    await _context.SaveChangesAsync();
    return true;
  }

  public async Task<Frequency> CreateFrequencyAsync(string frequencyType, int? intervalValue, List<int> days)
  {
    var freq = new Frequency
    {
      FrequencyType = frequencyType,
      IntervalValue = (int)intervalValue
    };
    await _context.Frequencies.AddAsync(freq);
    await _context.SaveChangesAsync();

    if (frequencyType.Equals("weekly", StringComparison.OrdinalIgnoreCase) || frequencyType.Equals("monthly", StringComparison.OrdinalIgnoreCase))
    {
      if (days == null)
      {
        throw new BadRequestException("Days must be provided");
      }
      foreach (var day in days)
      {
        var freqDetail = new FrequencyDetail
        {
          FrequencyId = freq.FrequencyId,
          DayOfMonth = frequencyType.Equals("monthly", StringComparison.OrdinalIgnoreCase) ? day : null,
          DayOfWeek = frequencyType.Equals("weekly", StringComparison.OrdinalIgnoreCase) ? day : null
        };
        await _context.FrequencyDetails.AddAsync(freqDetail);
      }

      await _context.SaveChangesAsync();
    }
    return freq;
  }

  public async Task<Frequency> UpdateFrequencyAsync(Frequency frequency, string frequencyType, int? intervalValue, List<int>? days)
  {
    frequency.FrequencyType = frequencyType;
    frequency.IntervalValue = (int)intervalValue;

    // Xóa FrequencyDetails cũ
    var oldDetails = _context.FrequencyDetails.Where(fd => fd.FrequencyId == frequency.FrequencyId);
    _context.FrequencyDetails.RemoveRange(oldDetails);

    // Thêm mới nếu cần
    if (frequencyType.Equals("weekly", StringComparison.OrdinalIgnoreCase) ||
        frequencyType.Equals("monthly", StringComparison.OrdinalIgnoreCase))
    {
      if (days != null)
      {
        foreach (var day in days)
        {
          var fd = new FrequencyDetail
          {
            FrequencyId = frequency.FrequencyId,
            DayOfWeek = frequencyType.Equals("weekly", StringComparison.OrdinalIgnoreCase) ? day : null,
            DayOfMonth = frequencyType.Equals("monthly", StringComparison.OrdinalIgnoreCase) ? day : null
          };
          await _context.FrequencyDetails.AddAsync(fd);
        }
      }
    }

    await _context.SaveChangesAsync();
    return frequency;
  }

  public async System.Threading.Tasks.Task UpdateTaskFrequencyAsync(int taskId, int frequencyId)
  {
    var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
    if (task == null)
      throw new NotFoundException("Task not found");

    task.FrequencyId = frequencyId;
    await _context.SaveChangesAsync();
  }


  public async System.Threading.Tasks.Task AssignUsersToTaskAsync(int taskId, List<int> userIds)
  {
    if (userIds == null || !userIds.Any()) return;

    // Load task hiện tại từ DbContext
    var task = await _context.Tasks
        .Include(t => t.Users) // collection navigation property
        .FirstOrDefaultAsync(t => t.TaskId == taskId);

    if (task == null)
      throw new NotFoundException("Task not found");

    // Load user từ DbContext
    var users = await _context.Users
        .Where(u => userIds.Contains(u.UserId))
        .ToListAsync();

    foreach (var user in users)
    {
      // Gán user vào task nếu chưa có
      if (!task.Users.Any(a => a.UserId == user.UserId))
      {
        task.Users.Add(user);
      }
    }

    await _context.SaveChangesAsync();
  }



  public async System.Threading.Tasks.Task AssignUnitsToTaskAsync(int taskId, List<int> unitIds)
  {
    if (unitIds == null || !unitIds.Any()) return;

    var task = await _context.Tasks
        .Include(t => t.Taskunitassignments)
        .FirstOrDefaultAsync(t => t.TaskId == taskId);

    if (task == null)
      throw new NotFoundException("Task not found");

    var units = await _context.Units
        .Where(u => unitIds.Contains(u.UnitId))
        .ToListAsync();

    foreach (var unit in units)
    {
      if (!task.Taskunitassignments.Any(tu => tu.UnitId == unit.UnitId))
      {
        task.Taskunitassignments.Add(new Taskunitassignment
        {
          TaskId = task.TaskId,
          UnitId = unit.UnitId
        });
      }
    }

    await _context.SaveChangesAsync();
  }

  public async Task<TaskModel?> GetByIdWithUsersAndUnitsAsync(int taskId)
  {
    return await _context.Tasks
        .Include(t => t.Users)
        .Include(t => t.Taskunitassignments)
        .FirstOrDefaultAsync(t => t.TaskId == taskId);
  }

    public async Task<List<int>> GetAssignedTaskIdsForUserAsync(int userId, string? search)
    {
        try
        {
            var taskIdsQuery = @"
        SELECT DISTINCT t1.taskId 
        FROM dbo.task t1
        LEFT JOIN dbo.taskassignees t2 ON t2.TaskId = t1.taskId
        LEFT JOIN dbo.taskunitassignment t3 ON t3.TaskId = t1.taskId 
        LEFT JOIN dbo.unituser u ON u.unitId = t3.UnitId 
        WHERE t1.isDeleted = 0 
        AND (
            t2.UserId = @p0 
            OR (u.userId = @p1 AND u.[level] = 1)
        )";

            if (!string.IsNullOrWhiteSpace(search))
            {
                taskIdsQuery += " AND t1.title LIKE @p2";
            }

            taskIdsQuery += " ORDER BY t1.createdAt DESC";

            var parameters = new List<object> { userId, userId };
            if (!string.IsNullOrWhiteSpace(search))
            {
                parameters.Add($"{search}%");
            }

            var taskIds = await _context.Database
                .SqlQueryRaw<int>(taskIdsQuery, parameters.ToArray())
                .ToListAsync();

            return taskIds;
        }
        catch (Exception ex)
        {
            var fallbackQuery = _context.Tasks
                .Where(t => t.IsDeleted == false &&
                            (t.Users.Any(u => u.UserId == userId) ||
                             t.Taskunitassignments.Any(tu => tu.Unit.Unitusers.Any(uu => uu.UserId == userId && uu.Level == 1))));

            if (!string.IsNullOrWhiteSpace(search))
            {
                fallbackQuery = fallbackQuery.Where(t => t.Title.Contains(search));
            }

            return await fallbackQuery
                .Select(t => t.TaskId)
                .OrderByDescending(t => t)
                .ToListAsync();
        }
    }

}

