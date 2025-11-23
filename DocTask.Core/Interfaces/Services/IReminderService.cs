using DocTask.Core.Dtos.Reminders;
using DocTask.Core.Paginations;
using ReminderModel = DocTask.Core.Models.Reminder;

namespace DocTask.Core.Interfaces.Services;

public interface IReminderService
{

    // nhac nho cho unit
    Task<ReminderModel> CreateReminderUnit(int subTask, int unitId, string message, int createBy);
    Task<ReminderModel> CreateReminderWithNotificationAsync(int taskId, int createdBy, int userId, string message);

    //gui realtime
    Task SendRealTimeNotificationAsync(int userId, string title, string message, object? data = null);
    Task SendMultipleNotificationsAsync(List<int> userIds, string title, string message, object? data = null);

    // Nhac nho den gan han
    //Task CreateAndUpdateReminderAsync(int subTaskId, int userId, DateTime dueDate);
    //Task <ReminderDto> CreateRemider1Async(RemiderRequest request);



    Task<ReminderModel> CreateAsync(ReminderDto dto);
    Task<PaginatedList<ReminderDto>> GetAsync(PageOptionsRequest pageOptions, int? taskId = null, int? userId = null, bool? isNotified = null);
    Task MarkNotifiedAsync(int reminderId);
    Task<PaginatedList<ReminderDetailDto>> GetRemindersByUserId(int userId, PageOptionsRequest pageOptions);
    Task<ReminderModel> CreateReminderAsync(int taskId,int createdBy, int userId, string message);
    Task<bool> DeleteReminderAsync(int reminderId);
    Task<int> DeleteByTaskIdAsync(int taskId);
    Task<int> GetUnreadReminderCount(int userId);
    Task<bool> ReadReminder(int userId, int reminderId);
}


