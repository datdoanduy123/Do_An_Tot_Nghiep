using DocTask.Core.Dtos.Reminders;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using ReminderModel = DocTask.Core.Models.Reminder;
using Task = System.Threading.Tasks.Task;
using TaskModel = DocTask.Core.Models.Task;


namespace DocTask.Core.Interfaces.Repositories;

public interface IReminderRepository
{

    //Reminder for Unit
    Task<Unit?> GetUnitUserAsync(int unitId);
    Task<TaskModel?> GetTask(int subTaskId);
    Task<int?> GetUnitHeadUserById(int unitId);
    Task<ReminderModel?> GetUnitUserByIdAsync(int reminderId);
    Task CreateReminderUnitAsync( int reminderId, int unitId);   



    Task<ReminderModel> CreateAsync(ReminderDto dto);
    Task<PaginatedList<ReminderDto>> GetAsync(PageOptionsRequest pageOptions, int? taskId = null, int? userId = null, bool? isNotified = null);
    Task MarkNotifiedAsync(int reminderId);
    Task<PaginatedList<ReminderModel>> GetRemindersByUserIdAsync(int userId, PageOptionsRequest options);
    Task<ReminderModel?> GetByIdAsync(int reminderId);
    Task<bool> DeleteAsync(int reminderId);
    Task<ReminderModel> CreateReminderAsync(int taskId,int createdBy, int userId, string message);
    Task<int> DeleteByTaskIdAsync(int taskId);

    Task<List<ReminderModel>> GetDueRemindersAsync();
    Task<ReminderModel> CreateReminder1Async(ReminderModel reminder);
    Task<int> CountUnreadReminder(int userId);
    Task<ReminderModel> UpdateAsync(ReminderModel reminder);
}


