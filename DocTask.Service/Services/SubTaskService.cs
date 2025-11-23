using DocTask.Core.Dtos.SubTasks;
using DocTask.Core.Dtos.Units;
using DocTask.Core.Dtos.Users;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using DocTask.Data.Repositories;
using DocTask.Service.Mappers;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using System.Threading.Channels;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using TaskEntity = DocTask.Core.Models.Task;
using UserDto = DocTask.Core.Dtos.Units.UserDto;

namespace DocTask.Service.Services;

public class SubTaskService : ISubTaskService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ISubTaskRepository _subTaskRepository;
    private readonly IUserRepository _userRepository;
    private readonly IFrequencyDetailRepository _frequencyDetailRepository;
    private readonly IFrequencyRepository _frequencyRepository;
    private readonly IReminderService _reminderService;
    private readonly IReminderRepository _reminderRepository;
    private readonly IUnitRepository _unitRepository;
    private readonly List<string> FREQUENCY_TYPES = ["daily", "weekly", "monthly"];
    private readonly ITaskPermissionService _taskPermissionService;
    // thuy 
    private readonly IProgressRepository _progressRepository;


    public SubTaskService(ISubTaskRepository subTaskRepository, ITaskRepository taskRepository, IUserRepository userRepository, IFrequencyDetailRepository frequencyDetailRepository, IFrequencyRepository frequencyRepository, IReminderService reminderService, IUnitRepository unitRepository, ITaskPermissionService taskPermissionService, IProgressRepository progressRepository, IReminderRepository reminderRepository)
    {
        _subTaskRepository = subTaskRepository;
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _frequencyDetailRepository = frequencyDetailRepository;
        _frequencyRepository = frequencyRepository;
        _reminderService = reminderService;
        _unitRepository = unitRepository;
        _taskPermissionService = taskPermissionService;
        _progressRepository = progressRepository;
        _reminderRepository = reminderRepository;
    }

    public async Task<SubTaskDto> CreateAsync(int parentTaskId, CreateSubTaskRequest request, int userId)
    {
        if (request.StartDate.Day < DateTime.Now.Day || request.StartDate > request.DueDate)
            throw new BadRequestException("StartDate must be earlier than or equal to DueDate.");

        // Check days
        if (request.Frequency != "daily" &&
            request.Days != null &&
            request.Days.Any(day =>
                day < 1 ||
                (request.Frequency.Equals("weekly") && day > 7) ||
                (request.Frequency.Equals("monthly") && day > 30)))
                {
                    throw new BadRequestException("Invalid days");
                }


        if (!FREQUENCY_TYPES.Contains(request.Frequency.ToLower()))
            throw new BadRequestException("Invalid frequency");

        var assignableUsers = await GetAssignableUsers(userId);
        if (!ValidateAssignedUsers(request.AssignedUserIds, assignableUsers))
            throw new BadRequestException("Assigned users are invalid.");

        var assignableUnits = await GetAssignableUnits(userId);
        if (!ValidateAssignedUnits(request.AssignedUnitIds, assignableUnits))
            throw new BadRequestException("Assigned units are invalid.");

        if (request.AssignedUnitIds.Count == 0 && request.AssignedUserIds.Count == 0)
            throw new BadRequestException("Assigned units or assignable units are invalid.");

        // Xác thực Task cha có không ?
        var parentTask = await _subTaskRepository.GetByIdAsync(parentTaskId);
        if (parentTask == null || parentTask.AssignerId != userId)
            throw new ArgumentException("Invalid request");

        if (request.StartDate.Day < parentTask.StartDate?.Day || request.DueDate.Day > parentTask.DueDate?.Day)
            throw new BadRequestException("The period must be contained the by parent task period");

        var createdSubTask = await _subTaskRepository.CreateSubtaskAsync(parentTaskId, request, userId);

        if (createdSubTask == null)
            throw new InvalidOperationException("Failed to retrieve created subtask");

        // Fetch the created task with assigned users to get the complete
        // 
        var taskWithUsers = await _subTaskRepository.GetByIdWithUsersAndUnitsAsync(createdSubTask.TaskId);
        if (taskWithUsers == null)
            throw new InvalidOperationException("Failed to retrieve created subtask");

        // Tạo nhắc nhở cho tất cả người được gán (dùng for để hỗ trợ nhiều người dùng)
        //await CreateAssignmentRemindersAsync(taskWithUsers, "Bạn đã được giao một công việc mới");
        await CreateAssignmentRemindersAsync(taskWithUsers);

        // Convert entity to DTO to avoid circular references
        return taskWithUsers.ToSubTaskDto();
    }
    // Cập nhật
    public async Task<SubTaskDto?> UpdateSubtask(int userId, int subTaskId, UpdateSubTaskRequest request)
    {
        if (request is { StartDate: not null, DueDate: not null } && (request.StartDate.Value.Day < DateTime.Now.Day || request.StartDate > request.DueDate))
            throw new BadRequestException("StartDate must be earlier than or equal to DueDate.");

        if (request.Frequency != null && !FREQUENCY_TYPES.Contains(request.Frequency.ToLower()))
            throw new BadRequestException("Invalid frequency");


        // Check days
        if (request is { Days: not null, Frequency: not null } && request.Days.Any(day =>
                day < 1 || request.Frequency.Equals("weekly") && day > 7 ||
                request.Frequency.Equals("monthly") && day > 30))
            throw new BadRequestException("Invalid days");

        // Check interval value && frequency
        if (request.Frequency != null && request.Frequency != "daily" && request.Days == null || request.Frequency != null && request.IntervalValue == null)
            throw new BadRequestException("Invalid frequency");

        var existingSubTask = await _subTaskRepository.GetByIdWithFrequenciesAsync(subTaskId);
        if (existingSubTask == null)
            throw new NotFoundException("Invalid task!");

        var parentTask = await _subTaskRepository.GetByIdAsync(existingSubTask.ParentTaskId.Value);
        if (parentTask == null || parentTask.AssignerId != userId)
            throw new BadRequestException("Invalid task!");

        if (request.StartDate?.Day < parentTask.StartDate?.Day || request.DueDate?.Day > parentTask.DueDate?.Day)
            throw new BadRequestException("The period must be contained the by parent task period");

        await _subTaskRepository.UpdateSubtaskAsync(existingSubTask, request);
        var updatedSubtask = await _subTaskRepository.GetByIdWithUsersAndUnitsAsync(existingSubTask.TaskId);

        if (updatedSubtask != null)
            await UpdateAssigmentReminderAsync(existingSubTask, updatedSubtask);

        return updatedSubtask?.ToSubTaskDto();
    }


    //Xóa
    public async Task<bool> DeleteAsync(int subTaskId, int userId)
    {
        //var existingSubTask = await _subTaskRepository.GetByIdAsync(subTaskId);
        var existingSubTask = await _subTaskRepository.GetByIdWithUsersAndUnitsAsync(subTaskId);

        if (existingSubTask == null)
            throw new NotFoundException("Sub-task không tồn tại");
        if (existingSubTask.ParentTaskId == null)
            throw new BadRequestException("Không dược xóa task cha");

        if (existingSubTask.AssignerId == null || existingSubTask.AssignerId != userId)
            throw new UnauthorizedException("Bạn không có quyền xóa sub-task này");

        var user = await _userRepository.GetByIdAsync(userId);
        var fullName = user.FullName ?? userId.ToString();
        var message = $"{fullName} đã xóa công việc {existingSubTask.Title}";

        var UnitAssignerName = await _unitRepository.GetUserUnitNameByIdAsync(user.UserId);


        var delete = await _subTaskRepository.DeleteAsync(subTaskId);
        if (!delete) return false;

        foreach(var u in existingSubTask.Users)
        {
            try
            {
                var reminder = await _reminderService.CreateReminderAsync(existingSubTask.TaskId,existingSubTask.AssignerId.Value, u.UserId, message);
                await _reminderService.SendRealTimeNotificationAsync
                    (
                        u.UserId,
                        "Xóa công việc",
                        message
                    );
            }
            catch (Exception ex) {
                
            }
        }

        foreach (var unitAssingment in existingSubTask.Taskunitassignments)
        {
            try
            {
                var unitId = unitAssingment.UnitId;
                var unit = await _reminderRepository.GetUnitUserAsync(unitId);
                
                if (unit == null)
                {
                    continue;
                }

                // Lay ten unitHeader
                var unitHeaderId = await _reminderRepository.GetUnitHeadUserById(unitId);
                //var fullNameHeader = await _userRepository.GetByIdAsync(unitHeaderId.Value);
                //var name = fullNameHeader?.FullName ?? fullNameHeader!.ToString();



                var messageUnit = $"{fullName} từ phòng ban {UnitAssignerName} đã xóa công việc {existingSubTask.Title} của phòng ban của bạn";

                await _reminderService.CreateReminderAsync(existingSubTask.TaskId,existingSubTask.AssignerId.Value, unitHeaderId.Value, messageUnit);

                await _reminderService.SendRealTimeNotificationAsync
                    (
                        unitHeaderId.Value,
                        "Xóa công việc",
                        messageUnit
                    );
            }
            catch (Exception ex) { }
        }
        return true;
    }

    public async Task<PaginatedList<SubTaskDto>> GetAllByParentIdAsync(int userId, int parentTaskId, PageOptionsRequest pageOptions, string? key)
    {
        var foundTask = await _taskRepository.GetTaskByIdAsync(parentTaskId);
        if (foundTask == null || foundTask.AssignerId != userId)
            throw new NotFoundException("Invalid task!");


        var paginatedSubTasks = await _subTaskRepository.GetAllByParentIdPaginatedAsync(parentTaskId, pageOptions, key);

        var dtoList = paginatedSubTasks.Items.Select(t => t.ToSubTaskDto()).ToList();

        return new PaginatedList<SubTaskDto>(dtoList, paginatedSubTasks.MetaData);
    }

    public async Task<PaginatedList<SubTaskDto>> GetByAssignedUserIdPaginatedAsync(int userId, string? key, PageOptionsRequest pageOptions)
    {
        var paginatedSubTasks = await _subTaskRepository.GetByAssignedUserIdPaginatedAsync(userId, key, pageOptions);
        var dtoList = paginatedSubTasks.Items.Select(SubTaskMapper.ToSubTaskDto).ToList();

        return new PaginatedList<SubTaskDto>(dtoList, paginatedSubTasks.MetaData);
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

    public async Task<List<SubTaskDto>> GetByKeywordAsync(string keyword)
    {
        var subTasks = await _subTaskRepository.GetByKeywordAsync(keyword);
        return subTasks.Select(SubTaskMapper.ToSubTaskDto).ToList();
    }

    public async Task AssignUsersToTaskAsync(int taskId, List<int> userIds)
    {
        await _subTaskRepository.AssignUsersToTaskAsync(taskId, userIds);
    }

    //public async Task<List<int>> GetAssignedUserIdsAsync(int taskId)
    //{
    //    return await _subTaskRepository.GetAssignedUserIdsAsync(taskId);
    //}

    public async Task RemoveUserFromTaskAsync(int taskId, int userId)
    {
        await _subTaskRepository.RemoveUserFromTaskAsync(taskId, userId);
    }

    private async Task CreateAssignmentRemindersAsync(TaskEntity subtask)
    {

        string assignerName = null; 

        if (subtask.AssignerId.HasValue)
        {
            var assigner = await _userRepository.GetByIdAsync(subtask.AssignerId.Value);
            assignerName = assigner?.FullName ?? subtask.AssignerId.Value.ToString();
        }

        var message = $"{assignerName} đã giao công việc '{subtask.Title}' cho bạn";
        foreach (var user in subtask.Users)
        {
            try
            {
                var reminder = await _reminderService.CreateReminderAsync(
                    subtask.TaskId,
                    subtask.AssignerId.Value,
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
                        taskId = subtask.TaskId,
                        TaskTitle = subtask.Title,
                        subtask.Description,
                        subtask.StartDate,
                        DueDate = subtask.DueDate,
                        subtask.Percentagecomplete,
                        reminder.IsRead,
                        AssignedBy = assignerName,
                        AssignerId = subtask.AssignerId
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send notification to user {user.UserId}: {ex.Message}");
            }
        }

        // Gửi thông báo cho Unit
        foreach (var unitAssignment in subtask.Taskunitassignments)
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

                var unitMessage = $"{assignerName} đã giao công việc '{subtask.Title}' cho đơn vị {unit.UnitName}";

                var reminder = await _reminderService.CreateReminderAsync(
                    subtask.TaskId,
                    subtask.AssignerId.Value,
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
                        taskId = subtask.TaskId,
                        TaskTitle = subtask.Title,
                        subtask.Description,
                        subtask.StartDate,
                        DueDate = subtask.DueDate,
                        subtask.Percentagecomplete,
                        reminder.IsRead,
                        AssignedBy = assignerName,
                        AssignerId = subtask.AssignerId,
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

    private async Task UpdateAssigmentReminderAsync(TaskEntity oldTask, TaskEntity newTask)
    {
        var updateChanges = DetectChanges(oldTask, newTask);
        if (updateChanges.Count == 0) return; // No changes detected

        var updateAssignedUsers = newTask.Users.ToList();
        foreach (var user in updateAssignedUsers)
        {
            var message = $"Công việc: {newTask.Title} đã được cập nhật: {string.Join(", ", updateChanges)}";
            if (message.Length > 255) message = message.Substring(0, 255);

            try
            {
                await _reminderService.CreateReminderAsync(newTask.TaskId,newTask.AssignerId.Value, user.UserId, message);

                await _reminderService.SendRealTimeNotificationAsync(
                    user.UserId,
                    "Công việc được cập nhật",
                    message,
                    new
                    {
                        TaskId = newTask.TaskId,
                        TaskTitle = newTask.Title,
                        newTask.StartDate,
                        newTask.DueDate,
                        Frequency = newTask.Frequency != null
                            ? new { newTask.Frequency.FrequencyId, newTask.Frequency.FrequencyType, newTask.Frequency.IntervalValue }
                            : null
                    });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create reminder for user {user.UserId}: {ex.Message}");
            }
        }

    }

    public async Task<SubTaskDto> GetSubTaskUnitByIdAsync(int subTaskId)
    {
        var subTask = await _subTaskRepository.GetByIdWithUsersAndUnitsAsync(subTaskId);
        if (subTask == null)
            throw new NotFoundException("Task không tồn tại");

        // Lấy thông tin người đứng đầu của các unit được giao
        var unitLeaders = new List<DocTask.Core.Dtos.Users.UserDto>(); // Dùng fully qualified name

        foreach (var unitAssignment in subTask.Taskunitassignments)
        {
            var leader = await _unitRepository.GetLeaderOfUnit(unitAssignment.UnitId);
            if (leader != null)
            {
                unitLeaders.Add(new DocTask.Core.Dtos.Users.UserDto // Dùng fully qualified name
                {
                    UserId = leader.UserId,
                    Username = leader.Username,
                    FullName = leader.FullName,
                    Email = leader.Email,
                });
            }
        }

        var dto = subTask.ToSubTaskDto();
        dto.AssignedUsers = unitLeaders;

        return dto;

    }

    private List<String> DetectChanges(TaskEntity oldTask, TaskEntity newTask)
    {
        var changes = new List<string>();
        if (oldTask.Title != newTask.Title)
            changes.Add("Title");
        if (oldTask.Description != newTask.Description)
            changes.Add("Description");
        if (oldTask.StartDate != newTask.StartDate)
            changes.Add("StartDate");
        if (oldTask.DueDate != newTask.DueDate)
            changes.Add("DueDate");
        if (oldTask.Status != newTask.Status)
            changes.Add("Status");
        if (oldTask.Priority != newTask.Priority)
            changes.Add("Priority");
        if (oldTask.Percentagecomplete != newTask.Percentagecomplete)
            changes.Add("Percentagecomplete");
        if (oldTask.FrequencyId != newTask.FrequencyId)
            changes.Add("Frequency");
        return changes;
    }

    private bool ValidateAssignedUsers(List<int> assignedUserIds, AssignableUsersResponseDto assignableUsers)
    {
        var peerIds = assignableUsers.peers.Select(p => p.UserId).ToList();
        var surbodinateIds = assignableUsers.subordinates.Select(p => p.UserId).ToList();
        var assignableIds = peerIds.Concat(surbodinateIds).ToList();

        foreach (var i in assignedUserIds)
        {
            if (!assignableIds.Contains(i))
                return false;
        }

        return true;
    }

    private bool ValidateAssignedUnits(List<int> assignedUnitIds, AssignableUnitsResponseDto assignableUsers)
    {
        var peerIds = assignableUsers.peers.Select(p => p.UnitId).ToList();
        var surbodinateIds = assignableUsers.surbodinates.Select(p => p.UnitId).ToList();
        var assignableIds = peerIds.Concat(surbodinateIds).ToList();

        foreach (var i in assignedUnitIds)
        {
            if (!assignableIds.Contains(i))
                return false;
        }

        return true;
    }

    public async Task<bool> ChangeParentTaskStatusAsync(int taskId, int userId, string status)
    {
        var tasks = await _subTaskRepository.GetSubTaskWithParentAsync(taskId);
        if (tasks == null)
            throw new NotFoundException("Parent Task không tồn tại");

        if (tasks.AssignerId != userId)
        {
            throw new NotFoundException("Bạn không có quyền");

        }
        var success = await _subTaskRepository.UpdateSubTaskStatus(taskId, status);
        return success;
    }

    public async Task<List<object>> GetAssignedUsersAsync(int taskId)
    {
        var foundTask = await _subTaskRepository.GetByIdWithUsersAndUnitsAsync(taskId);
        if (foundTask == null)
            throw new NotFoundException("Invalid task");

        if (foundTask.Users.Count != 0)
        {
            return foundTask.Users.Select(user => user.ToUserDto()).ToList<object>();
        }

        var result = new List<object>();

        foreach (var t in foundTask.Taskunitassignments)
        {
            var leader = await _unitRepository.GetLeaderOfUnit(t.UnitId);
            result.Add(new AssignedUnitDto
            {
                UnitId = t.Unit.UnitId,
                UnitName = t.Unit.UnitName,
                Org = t.Unit.Org.OrgName,
                Type = t.Unit.Type,
                Leader = leader?.ToUserDto()
            });
        }

        return result;

    }

    public async Task<SubTaskDto> GetSubTaskByIdAsync(int subTaskId)
    {
        var subTask = await _subTaskRepository.GetByIdAsync(subTaskId);
        if (subTask == null) throw new NotFoundException("Khong tim thay subtask");

        return SubTaskMapper.ToSubTaskDto(subTask);
    }

    public async Task<bool> ChangeSubTaskStatusAsync(int taskId, int userId, string status)
    {
        var tasks = await _subTaskRepository.GetSubTaskWithParentAsync(taskId);
        if (tasks == null)
            throw new NotFoundException("Sub-task không tồn tại");
        if (tasks.AssigneeId != userId)
        {
            throw new NotFoundException("Bạn không có quyền");

        }
        var success = await _subTaskRepository.UpdateSubTaskStatus(taskId, status);
        return success;
    }
}
