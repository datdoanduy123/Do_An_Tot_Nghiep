using DocTask.Core.Dtos.Reminders;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Paginations;
using DocTask.Data;
using DocTask.Service.Mappers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using ReminderModel = DocTask.Core.Models.Reminder;

namespace DocTask.Service.Services;

public class ReminderService : IReminderService
{
    private readonly IReminderRepository _repo;
    private readonly IUserRepository _userRepository;
    private readonly ApplicationDbContext _dbContext;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<ReminderService> _logger;
    private readonly ISubTaskRepository _subTaskRepository;
    private readonly IUnitRepository _unitRepository;

    public ReminderService(IReminderRepository repo, IUserRepository userRepository, ApplicationDbContext dbContext,
                       IHubContext<NotificationHub> hubContext, ILogger<ReminderService> logger, ISubTaskRepository subTaskRepository, IUnitRepository unitRepository)
    {
        _repo = repo;
        _userRepository = userRepository;
        _dbContext = dbContext;
        _hubContext = hubContext;
        _logger = logger;
        _subTaskRepository = subTaskRepository;
        _unitRepository = unitRepository;
    }

    public Task<ReminderModel> CreateAsync(ReminderDto dto) => _repo.CreateAsync(dto);
    //public async Task<ReminderDto> CreateRemider1Async(RemiderRequest request)
    //{
    //    var remider = ReminderMapper.ToRemider(request);
    //    await _repo.CreateReminder1Async(remider);
    //    return ReminderMapper.FromRemider(remider);
    //}

    public Task<PaginatedList<ReminderDto>> GetAsync(PageOptionsRequest pageOptions, int? taskId = null, int? userId = null, bool? isNotified = null)
        => _repo.GetAsync(pageOptions, taskId, userId, isNotified);

    public Task MarkNotifiedAsync(int reminderId) => _repo.MarkNotifiedAsync(reminderId);

    public async Task<PaginatedList<ReminderDetailDto>> GetRemindersByUserId(int userId, PageOptionsRequest pageOptions)
    {
        // Validate user exists
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
        {
            throw new NotFoundException("Người dùng không tồn tại.");
        }

        var modelPagedList = await _repo.GetRemindersByUserIdAsync(userId, pageOptions);

        return new PaginatedList<ReminderDetailDto>
        {
            Items = modelPagedList.Items.Select(i => i.ToReminderDetailDto(userId)).ToList(),
            MetaData = modelPagedList.MetaData
        };
    }

    public async Task<ReminderModel> CreateReminderAsync(int taskId, int createdBy, int userId, string message)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("Người dùng không tồn tại.");

        var task = await _dbContext.Tasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TaskId == taskId);

        if (task == null)
            throw new NotFoundException("Nhiệm vụ không tồn tại.");

        return await _repo.CreateReminderAsync(taskId, createdBy, userId, message);
    }

    public async Task<bool> DeleteReminderAsync(int reminderId)
    {
        // Validate reminder exists
        var reminder = await _repo.GetByIdAsync(reminderId);
        if (reminder == null)
        {
            throw new NotFoundException("Nhắc nhở không tồn tại.");
        }

        return await _repo.DeleteAsync(reminderId);
    }

    public Task<int> DeleteByTaskIdAsync(int taskId)
    {
        return _repo.DeleteByTaskIdAsync(taskId);
    }

    public Task<int> GetUnreadReminderCount(int userId)
    {
        return _repo.CountUnreadReminder(userId);
    }

    public async Task<bool> ReadReminder(int userId, int reminderId)
    {
        var reminder = await _repo.GetByIdAsync(reminderId);
        if (reminder == null || reminder.UserId != userId)
            throw new BadRequestException("Invalid Reminder");
        
        reminder.IsRead = true;
        await _repo.UpdateAsync(reminder);
        await _hubContext.Clients.Group($"user-{userId}")
            .SendAsync("ReminderRead", reminderId);
        return true;
    }

    public async Task SendRealTimeNotificationAsync(int userId, string? title, string message, object? data = null)
    {
        try
        {
            var notification = new
            {
                Title = title,
                Message = message,
                Timestamp = DateTime.UtcNow,
                Data = data
            };

            await _hubContext.Clients.Group($"user-{userId}")
                .SendAsync("ReceiveNotification", notification);

            _logger.LogInformation($"Sent notification to user {userId}: {title}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending notification to user {userId}");
        }
    }

    public Task SendMultipleNotificationsAsync(List<int> userIds, string title, string message, object? data = null)
    {
        throw new NotImplementedException();
    }

    //public async Task CreateAndUpdateReminderAsync(int subTaskId, int userId, DateTime dueDate)
    //{
    //    await _repo.DeleteByTaskIdAsync(subTaskId);

    //    var remider = new ReminderModel
    //    {
    //        Taskid = subTaskId,
    //        UserId = userId,
    //        Triggertime = dueDate.AddDays(-1).Date.AddHours(8),
    //        Title = "Nhắc nhở nhiệm vụ",
    //        Message = "Bạn có công việc sắp đến hạn",
    //        Isnotified = false,
    //        Createdat = DateTime.Now,
    //        Isauto = true,
    //        Createdby = userId
    //    };

    //    await _repo.CreateReminder1Async(remider);
    //}

    public async Task<ReminderModel> CreateReminderUnit(int subTaskId, int unitId, string message, int createBy)
    {
        var subTask = await _repo.GetTask(subTaskId);
        if(subTask == null)
        {
            throw new NotFoundException("Nhiem vu nay khong ton tai");
        }
        var unit = await _repo.GetUnitUserAsync(unitId);
        if (unit == null)
        {
            throw new NotFoundException("Phong ban nay khong ton tai");
        }

        var unitheader = await _repo.GetUnitHeadUserById(unitId);
        if (unitheader == null) return null;

        var assigner = await _userRepository.GetByIdAsync(createBy);
        var assignerName = assigner?.FullName ?? $"User {createBy}";
        var assignerUnit = await _repo.GetUnitUserAsync(createBy);
        var assignerUnitName = assignerUnit?.UnitName ?? "chưa xác định";

        message = $"{assignerName} từ phòng ban {assignerUnitName} đã nhắc nhở công việc {subTask.Title} cho đơn vị {unit.UnitName}";


 

        var unitHeadUserId = await _repo.GetUnitHeadUserById(unitId);
        if (unitHeadUserId == null) return null;

        var reminder = new ReminderModel
        {
            Taskid = subTaskId,
            Message = message,
            UserId = unitHeadUserId.Value, // Assign to unit head
            Createdby = createBy,
            Isauto = false,
            Isnotified = false,
            IsRead = false
        };

        await _repo.CreateReminder1Async(reminder);

        await _repo.CreateReminderUnitAsync(reminder.Reminderid, unitId);

        await SendRealTimeNotificationAsync(
            unitHeadUserId.Value,
            title: null,
            message: message,
            new
            {
                unitHeadUserId = unitHeadUserId.Value,
                ReminderId = reminder.Reminderid,
                TaskId = subTaskId,
                UnitId = unitId,
                Status = subTask.Title,
                subTask = subTask.Description,
                startDate = subTask.StartDate,
                DueDate = subTask.DueDate,
                Percentagecomplete = subTask.Percentagecomplete,
                IsRead = reminder.IsRead,

            });

        _logger.LogInformation($"Đã tạo nhắc nhở cho Unit {unitId} và đã giao tới {unitHeadUserId}");
        return reminder;

    }

    public async Task<ReminderModel> CreateReminderWithNotificationAsync( int taskId, int createdBy, int userId, string message)
   
    {
        var user = await _userRepository.GetByIdAsync(createdBy);
        if (user == null)
            throw new NotFoundException("Không tìm thấy User");

        var existingSubTask = await _subTaskRepository.GetByIdAsync(taskId);
        if (existingSubTask == null)
            throw new NotFoundException("Không tìm thấy Task");

        var fullName = user.FullName ?? createdBy.ToString();
        var finalMessage = message;

        var UnitName = await _unitRepository.GetUserUnitNameByIdAsync(createdBy);

        var reminder = await CreateReminderAsync(taskId, createdBy, userId, finalMessage);

        await SendRealTimeNotificationAsync(
            userId,  
            $"Nhắc nhở từ {fullName} thuộc đơn vị {UnitName} về công việc {existingSubTask.Title}",
            finalMessage,
            new
            {
                reminderId = reminder.Reminderid,
                taskId = existingSubTask.TaskId,
                taskTitle = existingSubTask.Title,
                taskDescription = existingSubTask.Description,
                percentageComplete = existingSubTask.Percentagecomplete,
                status = existingSubTask.Status,
                isRead = reminder.IsRead,
                createdBy = fullName
            }
        );

        return reminder;
    }
}







