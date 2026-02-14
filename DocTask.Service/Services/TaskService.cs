using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Dtos.Units;
using DocTask.Core.Dtos.Users;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using DocTask.Data;
using DocTask.Service.Mappers;
using Microsoft.EntityFrameworkCore;
using Task = System.Threading.Tasks.Task;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Service.Services;

/// <summary>
/// Service quản lý Task: CRUD + Assign + Auto-assign + Schedule generation.
/// </summary>
public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISubTaskRepository _subTaskRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitRepository _unitRepository;
    private readonly IReminderService _reminderService;
    private readonly IReminderRepository _reminderRepository;
    private readonly ApplicationDbContext _dbContext;
    private readonly IAutoAssignmentService _autoAssignmentService;

    public TaskService(
        ITaskRepository taskRepository,
        ISubTaskRepository subTaskRepository,
        IUserRepository userRepository,
        IUnitRepository unitRepository,
        IReminderService reminderService,
        IReminderRepository reminderRepository,
        IAutoAssignmentService autoAssignmentService,
        ApplicationDbContext dbContext)
    {
        _taskRepository = taskRepository;
        _subTaskRepository = subTaskRepository;
        _userRepository = userRepository;
        _unitRepository = unitRepository;
        _reminderService = reminderService;
        _reminderRepository = reminderRepository;
        _autoAssignmentService = autoAssignmentService;
        _dbContext = dbContext;
    }

    public async Task<PaginatedList<TaskDto>> GetAll(PageOptionsRequest pageOptions, string? key, int userId)
    {
        var paginatedListModel = await _taskRepository.GetAllAsync(pageOptions, key, userId);

        return new PaginatedList<TaskDto>
        {
            MetaData = paginatedListModel.MetaData,
            Items = paginatedListModel.Items.Select(t => t.ToTaskDto()).ToList(),
        };
    }

    public async Task<TaskDto?> CreateTaskAsync(CreateTaskDto taskDto, int userId)
    {
        // Kiểm tra null cho StartDate và DueDate
        if (!taskDto.StartDate.HasValue || !taskDto.DueDate.HasValue)
            throw new BadRequestException("StartDate and DueDate are required.");

        if (taskDto.StartDate.Value.Day < DateTime.Now.Day || taskDto.StartDate > taskDto.DueDate)
            throw new BadRequestException("StartDate must be earlier than or equal to DueDate.");

        // Lấy danh sách user và unit được phép giao việc
        var assignableUsersIds = await GetAssignableUsers(userId);
        if (!ValidateAssignedUsers(taskDto.AssignedUsersIds, assignableUsersIds))
            throw new BadRequestException("Assigned users are invalid.");

        var assignableUnitIds = await GetAssignableUnits(userId);
        if (!ValidateAssignedUnits(taskDto.AssignedUnitIds, assignableUnitIds))
            throw new BadRequestException("Assigned units are invalid.");

        bool hasAssignees = (taskDto.AssignedUsersIds != null && taskDto.AssignedUsersIds.Any()) ||
                            (taskDto.AssignedUnitIds != null && taskDto.AssignedUnitIds.Any());

        // Nếu có assign → validate Frequency/Days
        if (hasAssignees)
        {
            if (string.IsNullOrEmpty(taskDto.Frequency))
                throw new BadRequestException("Frequency is required when task has assignees.");

            if (taskDto.Frequency.Equals("weekly", StringComparison.OrdinalIgnoreCase) ||
                taskDto.Frequency.Equals("monthly", StringComparison.OrdinalIgnoreCase))
            {
                // Weekly hoặc Monthly phải có Days
                if (taskDto.Days == null || !taskDto.Days.Any())
                    throw new BadRequestException("Days are required for weekly or monthly frequency.");

                // Validate từng day
                if (taskDto.Days.Any(day =>
                    day < 1 ||
                    (taskDto.Frequency.Equals("weekly", StringComparison.OrdinalIgnoreCase) && day > 7) ||
                    (taskDto.Frequency.Equals("monthly", StringComparison.OrdinalIgnoreCase) && day > 31)))
                {
                    throw new BadRequestException("Invalid days for the selected frequency.");
                }
            }
        }

        // Tạo task cha
        var task = new TaskModel
        {
            Title = taskDto.Title,
            Description = taskDto.Description,
            AssignerId = userId,
            CreatedAt = DateTime.UtcNow,
            StartDate = taskDto.StartDate,
            DueDate = taskDto.DueDate,
        };

        var created = await _taskRepository.CreateTaskAsync(task);

        if (hasAssignees)
        {
            var freq = await _taskRepository.CreateFrequencyAsync(taskDto.Frequency, taskDto.IntervalValue, taskDto.Days);
            created.FrequencyId = freq.FrequencyId;
            await _taskRepository.UpdateTaskFrequencyAsync(created.TaskId, freq.FrequencyId);
            await _taskRepository.AssignUsersToTaskAsync(created.TaskId, taskDto.AssignedUsersIds);
            await _taskRepository.AssignUnitsToTaskAsync(created.TaskId, taskDto.AssignedUnitIds);
        }
    
        // Gửi reminder (tuỳ chọn)
        await CreateAssignmentRemindersAsync(created);

        // Trả về DTO
        var taskWithUsers = await _taskRepository.GetTaskByIdAsync(created.TaskId);
        if (taskWithUsers == null)
            throw new InternalServerErrorException("Không thể lấy thông tin task vừa tạo.");
        
        return taskWithUsers.ToTaskDto();
    }


    public async Task<TaskDto?> GetByIdAsync(int taskId, int userId)
    {
        var task = await _taskRepository.GetTaskByIdAsync(taskId);
        if (task == null)
        {
            throw new NotFoundException($"Không tìm thấy task với ID {taskId}.");
        }

        // Kiểm tra quyền truy cập - user phải là assigner hoặc assignee
        if (task.AssignerId != userId && task.AssigneeId != userId)
        {
            throw new UnauthorizedException("Bạn không có quyền xem task này.");
        }

        var taskWithRelations = await _taskRepository.GetByIdWithUsersAndUnitsAsync(taskId);
        if (taskWithRelations == null)
            throw new InternalServerErrorException("Không thể lấy thông tin task.");
        
        return taskWithRelations.ToTaskDto();
    }

    public async Task<TaskDto> UpdateTaskAsync(int taskId, UpdateTaskDto taskDto, int userId)
    {
        // Kiểm tra null cho StartDate và DueDate
        if (!taskDto.StartDate.HasValue || !taskDto.DueDate.HasValue)
            throw new BadRequestException("StartDate and DueDate are required.");

        if (taskDto.StartDate.Value.Day < DateTime.Now.Day || taskDto.StartDate > taskDto.DueDate)
            throw new BadRequestException("StartDate must be earlier than or equal to DueDate.");

        var existingTask = await _taskRepository.GetTaskByIdAsync(taskId);
        if (existingTask == null)
        {
            throw new NotFoundException($"Không tìm thấy task với ID {taskId}.");
        }
        if (existingTask.AssignerId != userId)
        {
            throw new UnauthorizedException("Bạn không có quyền cập nhật task này.");
        }
        var updated = await _taskRepository.UpdateTaskAsync(taskId, taskDto);
        if (updated == null)
        {
            throw new InternalServerErrorException("Cập nhật task thất bại.");
        }

        // Kiểm tra task được giao ở db (kiểm tra null cho collections)
        var hasAssignees = (existingTask.Users?.Any() ?? false) || (existingTask.Taskunitassignments?.Any() ?? false);

        if (hasAssignees)
        {
            if (string.IsNullOrEmpty(taskDto.Frequency))
                throw new BadRequestException("Frequency required when task has assignees");

            if ((taskDto.Frequency.Equals("weekly", StringComparison.OrdinalIgnoreCase) ||
                 taskDto.Frequency.Equals("monthly", StringComparison.OrdinalIgnoreCase)) &&
                (taskDto.Days == null || !taskDto.Days.Any()))
                throw new BadRequestException("Days required for weekly/monthly frequency");
        }


        return updated.ToTaskDto();
    }

    public async Task DeleteTaskAsync(int taskId, int userId)
    {
        var task = await _taskRepository.GetTaskByIdAsync(taskId);
        if (task == null)
            throw new NotFoundException($"Invalid task");

        if (task.ParentTaskId != null)
            throw new ConflictException("Invalid task");

        // Kiểm tra quyền
        if (task.AssignerId != userId && task.AssigneeId != userId)
            throw new UnauthorizedAccessException("Bạn không có quyền xóa task này.");
        await _taskRepository.DeleteAsync(task);
    }
    public async Task<AssignableUsersResponseDto> GetAssignableUsers(int userId)
    {
        var result = new AssignableUsersResponseDto();

        var foundUser = await _userRepository.GetByIdAsync(userId);
        if (foundUser == null)
            throw new NotFoundException("Invalid user");

        if (foundUser.UserParent != null)
        {
            var peerModels = await _userRepository.GetAllByParentUserId(foundUser.UserParent.Value);
            result.peers = peerModels.Where(p => p.UserId != foundUser.UserId).Select(p => p.ToUserDto()).ToList();
        }

        var surbodinateModels = await _userRepository.GetAllByParentUserId(foundUser.UserId);
        result.subordinates = surbodinateModels.Select(s => s.ToUserDto()).ToList();

        return result;
    }

    public async Task<AssignableUnitsResponseDto> GetAssignableUnits(int userId)
    {
        var result = new AssignableUnitsResponseDto
        {
            surbodinates = [],
            peers = []
        };

        var foundUser = await _userRepository.GetByIdWithUnitUserAsync(userId);
        if (foundUser == null)
            throw new NotFoundException("Invalid user");

        // Kiểm tra null cho UnitUser trước khi truy cập Level
        if (foundUser.UnitUser == null || foundUser.UnitUser.Level > 1)
            return result;

        // Kiểm tra null cho UnitId trước khi truy cập .Value
        if (!foundUser.UnitId.HasValue)
            throw new BadRequestException("User không có UnitId.");

        var foundUnit = await _unitRepository.GetUnitByIdAsync(foundUser.UnitId.Value);
        if (foundUnit == null)
            throw new NotFoundException("Unit not found");

        if (foundUnit.UnitParent != null)
        {
            var peerModels = await _unitRepository.GetSubUnitsByParentUnitIdAsync(foundUnit.UnitParent.Value);
            result.peers = peerModels.Where(p => p.UnitId != foundUnit.UnitId).Select(p => p.ToUnitBasicDto()).ToList();
        }

        var surbodinateModels = await _unitRepository.GetSubUnitsByParentUnitIdAsync(foundUnit.UnitId);
        result.surbodinates = surbodinateModels.Select(s => s.ToUnitBasicDto()).ToList();

        return result;
    }

    private async Task CreateAssignmentRemindersAsync(TaskModel task)
    {

        string assignerName = null;

        if (task.AssignerId.HasValue)
        {
            var assigner = await _userRepository.GetByIdAsync(task.AssignerId.Value);
            assignerName = assigner?.FullName ?? task.AssignerId.Value.ToString();
        }

        var message = $"{assignerName} đã giao công việc '{task.Title}' cho bạn";
        foreach (var user in task.Users)
        {
            try
            {
                var reminder = await _reminderService.CreateReminderAsync(
                    task.TaskId,
                    task.AssignerId.Value,
                    user.UserId,
                    message
                );

                await _reminderService.SendRealTimeNotificationAsync(
                    user.UserId,
                    "Công việc mới được giao",
                    message,
                    new
                    {
                        reminder.Reminderid,
                        taskId = task.TaskId,
                        TaskTitle = task.Title,
                        task.Description,
                        task.StartDate,
                        DueDate = task.DueDate,
                        task.Percentagecomplete,
                        reminder.IsRead,
                        AssignedBy = assignerName,
                        AssignerId = task.AssignerId
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send notification to user {user.UserId}: {ex.Message}");
            }
        }

        // Gửi thông báo cho Unit
        foreach (var unitAssignment in task.Taskunitassignments)
        {
            try
            {
                var unitId = unitAssignment.UnitId;
                var unit = await _reminderRepository.GetUnitUserAsync(unitId);

                if (unit == null)
                {
                    Console.WriteLine($"Phòng ban {unitId} không tồn tại.");
                    continue;
                }

                var unitHeaderId = await _reminderRepository.GetUnitHeadUserById(unitId);

                if (unitHeaderId == null)
                {
                    Console.WriteLine($"Không tìm thấy người đứng đầu phòng ban {unitId}");
                    continue;
                }

                var unitMessage = $"{assignerName} đã giao công việc '{task.Title}' cho đơn vị {unit.UnitName}";

                var reminder = await _reminderService.CreateReminderAsync(
                    task.TaskId,
                    task.AssignerId.Value,
                    unitHeaderId.Value,
                    unitMessage
                );

                await _reminderService.SendRealTimeNotificationAsync(
                    unitHeaderId.Value,
                    "Công việc mới được giao cho đơn vị",
                    unitMessage,
                    new
                    {
                        reminder.Reminderid,
                        taskId = task.TaskId,
                        TaskTitle = task.Title,
                        task.Description,
                        task.StartDate,
                        DueDate = task.DueDate,
                        task.Percentagecomplete,
                        reminder.IsRead,
                        AssignedBy = assignerName,
                        AssignerId = task.AssignerId,
                        UnitId = unitId,
                        UnitName = unit.UnitName
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send notification to unit {unitAssignment.UnitId}: {ex.Message}");
            }
        }
    }
    private bool ValidateAssignedUsers(List<int> assignedUsers, AssignableUsersResponseDto assignableUsers)
    {
        // Kiểm tra null cho input parameters
        if (assignedUsers == null || !assignedUsers.Any())
            return true; // Không có user nào để validate

        if (assignableUsers == null)
            return false;

        // Kiểm tra null cho collections trước khi Select
        var peerIds = assignableUsers.peers?.Select(p => p.UserId).ToList() ?? new List<int>();
        var surbodinateIds = assignableUsers.subordinates?.Select(p => p.UserId).ToList() ?? new List<int>();
        var assignableIds = peerIds.Concat(surbodinateIds).ToList();

        foreach (var i in assignedUsers)
        {
            if (!assignableIds.Contains(i))
                return false;
        }

        return true;
    }

    private bool ValidateAssignedUnits(List<int> assignedUnits, AssignableUnitsResponseDto assignableUsers)
    {
        // Kiểm tra null cho input parameters
        if (assignedUnits == null || !assignedUnits.Any())
            return true; // Không có unit nào để validate

        if (assignableUsers == null)
            return false;

        // Kiểm tra null cho collections trước khi Select
        var peerIds = assignableUsers.peers?.Select(p => p.UnitId).ToList() ?? new List<int>();
        var surbodinateIds = assignableUsers.surbodinates?.Select(p => p.UnitId).ToList() ?? new List<int>();
        var assignableIds = peerIds.Concat(surbodinateIds).ToList();

        foreach (var i in assignedUnits)
        {
            if (!assignableIds.Contains(i))
                return false;
        }

        return true;
    }
    public async Task AssignUsersToTaskAsync(int taskId, List<int> userIds)
    {
        await _subTaskRepository.AssignUsersToTaskAsync(taskId, userIds);
    }

    // ================================================================
    //  MANUAL ASSIGN — Gán user/unit thủ công
    // ================================================================

    /// <summary>
    /// Gán user/unit cho task thủ công.
    /// PATCH semantics: null = không đổi, [] = clear.
    /// </summary>
    public async Task<TaskDto> ManualAssignAsync(int taskId, AssignTaskRequest request, int userId)
    {
        var task = await _taskRepository.GetTaskByIdAsync(taskId);
        if (task == null)
            throw new NotFoundException($"Task {taskId} không tồn tại.");

        // Chỉ assigner mới được gán
        if (task.AssignerId != userId)
            throw new UnauthorizedException("Bạn không có quyền gán người cho task này.");

        // PATCH: UserIds
        if (request.UserIds != null)
        {
            // Xóa assignments cũ rồi gán mới
            var existingUsers = task.Users?.ToList() ?? new List<User>();
            foreach (var u in existingUsers)
            {
                task.Users!.Remove(u);
            }
            await _dbContext.SaveChangesAsync();

            // Gán mới (nếu list không rỗng)
            if (request.UserIds.Any())
            {
                await _taskRepository.AssignUsersToTaskAsync(taskId, request.UserIds);
            }
        }

        // PATCH: UnitIds
        if (request.UnitIds != null)
        {
            // Xóa assignments cũ
            var existingUnits = await _dbContext.Taskunitassignments
                .Where(t => t.TaskId == taskId)
                .ToListAsync();
            _dbContext.Taskunitassignments.RemoveRange(existingUnits);
            await _dbContext.SaveChangesAsync();

            // Gán mới
            if (request.UnitIds.Any())
            {
                await _taskRepository.AssignUnitsToTaskAsync(taskId, request.UnitIds);
            }
        }

        // Tạo reminder thông báo
        var updatedTask = await _taskRepository.GetByIdWithUsersAndUnitsAsync(taskId);
        if (updatedTask != null)
        {
            await CreateAssignmentRemindersAsync(updatedTask);
        }

        Console.WriteLine($"✅ Manual assign task {taskId}: Users={request.UserIds?.Count ?? 0}, Units={request.UnitIds?.Count ?? 0}");

        var result = await _taskRepository.GetByIdWithUsersAndUnitsAsync(taskId);
        return result!.ToTaskDto();
    }

    // ================================================================
    //  AUTO ASSIGN — Smart Engine Integration
    // ================================================================

    public async Task<List<AssignmentProposalDto>> AutoAssignDraftAsync(int draftId)
    {
        return await _autoAssignmentService.ProposeAssignmentsForDraftAsync(draftId);
    }

    /// <summary>
    /// Tự động gợi ý + gán người dựa trên rule-based scoring.
    /// Backward compatibility or single task usage.
    /// </summary>
    public async Task<TaskDto> AutoAssignByScoreAsync(int taskId, int userId)
    {
        // ... (keep existing logic or redirect to new service)
        // For now, let's keep existing logic as a fallback or specific implementation
        // But better to use the new engine if we want consistency.
        // Let's redirect to use the new service for consistency.
        
        var proposals = await _autoAssignmentService.ProposeAssignmentsAsync(new List<int> { taskId });
        var best = proposals.OrderByDescending(p => p.MatchScore).FirstOrDefault();

        if (best == null || best.MatchScore < 0.4m)
            throw new BadRequestException("Không tìm thấy ứng viên phù hợp (Score < 0.4).");

        await AssignUsersToTaskAsync(taskId, new List<int> { best.AssignedUserId });
        
        // Update IsAutoAssigned
        var task = await _taskRepository.GetTaskByIdAsync(taskId);
        if (task != null)
        {
            task.IsAutoAssigned = true;
            await _taskRepository.UpdateTaskAsync(task.TaskId, new UpdateTaskDto()); // Just to save? need optimized update
             _dbContext.Tasks.Update(task);
            await _dbContext.SaveChangesAsync();
        }

        Console.WriteLine($"✅ Smart Assign task {taskId} to {best.AssignedUserName} (Score: {best.MatchScore:F2})");
        return (await _taskRepository.GetByIdWithUsersAndUnitsAsync(taskId))!.ToTaskDto();
    }

    // ================================================================
    //  GENERATE SCHEDULE — Sinh lịch nhắc theo tần suất
    // ================================================================

    /// <summary>
    /// Sinh batch reminders từ StartDate → DueDate theo tần suất.
    /// - weekly: mỗi tuần tạo 1 reminder (vào thứ Hai)
    /// - monthly: mỗi tháng 1 reminder (ngày 1)
    /// - daily: mỗi ngày 1 reminder
    /// Trả về số reminders đã tạo.
    /// </summary>
    public async Task<int> GenerateScheduleAsync(int taskId, string frequency, int userId)
    {
        var task = await _taskRepository.GetTaskByIdAsync(taskId);
        if (task == null)
            throw new NotFoundException($"Task {taskId} không tồn tại.");

        if (!task.StartDate.HasValue || !task.DueDate.HasValue)
            throw new BadRequestException("Task chưa có StartDate/DueDate. Vui lòng cập nhật trước.");

        var start = task.StartDate.Value;
        var end = task.DueDate.Value;

        // Tạo danh sách trigger dates
        var triggerDates = new List<DateTime>();
        var current = start;

        switch (frequency.ToLower())
        {
            case "daily":
                while (current <= end)
                {
                    triggerDates.Add(current);
                    current = current.AddDays(1);
                }
                break;

            case "weekly":
                // Nhảy đến thứ 2 đầu tiên
                while (current.DayOfWeek != DayOfWeek.Monday)
                    current = current.AddDays(1);
                while (current <= end)
                {
                    triggerDates.Add(current);
                    current = current.AddDays(7);
                }
                break;

            case "monthly":
                // Mỗi tháng vào ngày 1
                current = new DateTime(current.Year, current.Month, 1);
                if (current < start) current = current.AddMonths(1);
                while (current <= end)
                {
                    triggerDates.Add(current);
                    current = current.AddMonths(1);
                }
                break;

            default:
                throw new BadRequestException($"Frequency '{frequency}' không hợp lệ. Chọn: daily, weekly, monthly.");
        }

        // Tạo batch reminders
        int count = 0;
        foreach (var triggerDate in triggerDates)
        {
            var reminder = new Reminder
            {
                Taskid = taskId,
                Title = $"Nhắc nhở: {task.Title}",
                Message = $"Deadline báo cáo tiến độ: {triggerDate:dd/MM/yyyy}",
                Triggertime = triggerDate,
                Isauto = true,
                Createdby = userId,
                Createdat = DateTime.UtcNow,
                Isnotified = false,
                IsRead = false
            };
            _dbContext.Reminders.Add(reminder);
            count++;
        }

        await _dbContext.SaveChangesAsync();
        Console.WriteLine($"📅 Generated {count} {frequency} reminders for task {taskId}");

        return count;
    }
}