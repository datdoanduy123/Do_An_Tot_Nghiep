using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Dtos.Units;
using DocTask.Core.Dtos.Users;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Paginations;
using DocTask.Service.Mappers;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Service.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISubTaskRepository _subTaskRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitRepository _unitRepository;
    private readonly IReminderService _reminderService;
    private readonly IReminderRepository _reminderRepository;

    public TaskService(ITaskRepository taskRepository, ISubTaskRepository subTaskRepository, IUserRepository userRepository, IUnitRepository unitRepository, IReminderService reminderService, IReminderRepository reminderRepository)
    {
        _taskRepository = taskRepository;
        _subTaskRepository = subTaskRepository;
        _userRepository = userRepository;
        _unitRepository = unitRepository;
        _reminderService = reminderService;
        _reminderRepository = reminderRepository;
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
        return taskWithRelations.ToTaskDto();
    }

    public async Task<TaskDto> UpdateTaskAsync(int taskId, UpdateTaskDto taskDto, int userId)
    {
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

        // kiểm tra task được giao ở db
        var hasAssignees = existingTask.Users.Any() || existingTask.Taskunitassignments.Any();

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

        if (foundUser.UnitUser.Level > 1)
            return result;

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
        var peerIds = assignableUsers.peers.Select(p => p.UserId).ToList();
        var surbodinateIds = assignableUsers.subordinates.Select(p => p.UserId).ToList();
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
        var peerIds = assignableUsers.peers.Select(p => p.UnitId).ToList();
        var surbodinateIds = assignableUsers.surbodinates.Select(p => p.UnitId).ToList();
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
    
}