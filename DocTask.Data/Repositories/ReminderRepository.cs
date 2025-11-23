using System.Linq;
using DocTask.Core.Dtos.Reminders;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using Microsoft.EntityFrameworkCore;
using ReminderModel = DocTask.Core.Models.Reminder;
using Task = System.Threading.Tasks.Task;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Data.Repositories;

public class ReminderRepository : IReminderRepository
{
    private readonly ApplicationDbContext _context;
    public ReminderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ReminderModel> CreateAsync(ReminderDto dto)
    {
        var entity = new ReminderModel
        {
            Taskid = dto.Taskid,
            Periodid = dto.Periodid,
            Title = dto.Title,
            Message = dto.Message,
            Triggertime = dto.Triggertime,
            Isauto = dto.Isauto,
            Createdby = dto.Createdby,
            Notifiedat = dto.Notifiedat,
            Notificationid = dto.Notificationid,
            UserId = dto.UserId
        };
        _context.Reminders.Add(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public async Task<PaginatedList<ReminderDto>> GetAsync(PageOptionsRequest pageOptions, int? taskId = null, int? userId = null, bool? isNotified = null)
    {
        var query = _context.Reminders.OrderByDescending(r =>r.Createdat).AsQueryable();
        if (taskId.HasValue) query = query.Where(r => r.Taskid == taskId.Value);
        if (userId.HasValue) query = query.Where(r => r.UserId == userId.Value);
        if (isNotified.HasValue) query = query.Where(r => r.Isnotified == isNotified.Value);

        var projected = query.Select(r => new ReminderDto
        {
            Reminderid = r.Reminderid,
            Taskid = r.Taskid,
            Periodid = r.Periodid,
            Title = r.Title,
            Message = r.Message,
            Triggertime = r.Triggertime,
            Isauto = r.Isauto,
            Createdby = r.Createdby,
            Createdat = r.Createdat,
            Isnotified = r.Isnotified,
            Notifiedat = r.Notifiedat,
            Notificationid = r.Notificationid,
            UserId = r.UserId
        });

        return await projected.ToPaginatedListAsync(pageOptions);
    }

    public async Task MarkNotifiedAsync(int reminderId)
    {
        var entity = await _context.Reminders.FirstOrDefaultAsync(r => r.Reminderid == reminderId);
        if (entity == null) return;
        entity.Isnotified = true;
        entity.Notifiedat = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<PaginatedList<ReminderModel>> GetRemindersByUserIdAsync(int userId, PageOptionsRequest pageOptions)
    {
        return await _context.Reminders
            .Where(r => r.UserId == userId)
            .Include(r => r.Task)
            .Select(r => new ReminderModel
            {
                Reminderid = r.Reminderid,
                Title = r.Title,
                Message = r.Message,
                IsRead = r.IsRead,
                Createdat = r.Createdat,
                Createdby = r.Createdby,
                Task = new Core.Models.Task
                {
                    TaskId = r.Task.TaskId, 
                    Title = r.Task.Title,
                    Description = r.Task.Description,
                    Status = r.Task.Status,
                    StartDate = r.Task.StartDate,
                    DueDate = r.Task.DueDate,
                    ParentTaskId = r.Task.ParentTaskId,
                    AssignerId = r.Task.AssignerId,
                },
            })
            .OrderByDescending(r => r.Createdat)
            .ToPaginatedListAsync(pageOptions);
    }

    public async Task<ReminderModel?> GetByIdAsync(int reminderId)
    {
        return await _context.Reminders
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Reminderid == reminderId);
    }

    public async Task<bool> DeleteAsync(int reminderId)
    {
        var entity = await _context.Reminders
            .FirstOrDefaultAsync(r => r.Reminderid == reminderId);

        if (entity == null) return false;

        _context.Reminders.Remove(entity);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<ReminderModel> CreateReminderAsync(int taskId,int createdBy, int userId, string message)
    {
        var reminder = new ReminderModel
        {
            Taskid = taskId,
            Message = message,
            UserId = userId,
            Triggertime = DateTime.Now,
            Createdby = createdBy,
            Title = message,
            Isauto = false,
            Isnotified = false
        };

        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();
        return reminder;
    }

    public async Task<int> DeleteByTaskIdAsync(int taskId)
    {
        var reminders = await _context.Reminders
            .Where(r => r.Taskid == taskId)
            .ToListAsync();

        if (reminders.Count == 0) return 0;

        _context.Reminders.RemoveRange(reminders);
        return await _context.SaveChangesAsync();
    }

    public async Task<List<ReminderModel>> GetDueRemindersAsync()
    {
        var now = DateTime.UtcNow;

        return await _context.Reminders
            .Where(r => ( r.Isnotified == false || r.Isnotified == null)&& r.Triggertime <= now)
            .ToListAsync();
    }

    public async Task<ReminderModel> CreateReminder1Async(ReminderModel reminder)
    {
        await _context.Reminders.AddAsync(reminder);
        await _context.SaveChangesAsync();
        return reminder;
    }

    public async Task<int> CountUnreadReminder(int userId)
    {
        return await _context.Reminders.Where(r => r.UserId == userId && r.IsRead == false).CountAsync();
    }

    public async Task<ReminderModel> UpdateAsync(ReminderModel reminder)
    {
        _context.Reminders.Update(reminder);
        await _context.SaveChangesAsync();

        return reminder;
    }

    public async Task<Unit?> GetUnitUserAsync(int unitId)
    {
        return await _context.Units
            .Include(u => u.Reminderunits)
            .FirstOrDefaultAsync(u => u.UnitId == unitId);
    }

    public async Task<TaskModel?> GetTask(int subTaskId)
    {
        return await _context.Tasks
            .FirstOrDefaultAsync(t => t.TaskId == subTaskId);
    }

    public async Task<int?> GetUnitHeadUserById(int unitId)
    {
        var head = await _context.Unitusers
            .FirstOrDefaultAsync( uu => uu.UnitId == unitId && uu.Level == 1);
        return head?.UserId;
    }

    public async Task<ReminderModel?> GetUnitUserByIdAsync(int reminderId)
    {
        return await _context.Reminders
            .Include(r => r.Reminderunits)
                .ThenInclude( ru => ru.Unit)
             .FirstOrDefaultAsync( r => r.Reminderid == reminderId);
    }

    public async Task CreateReminderUnitAsync(int reminderId, int unitId)
    {
        var reminderUnit = new Reminderunit
        {
            Reminderid = reminderId,
            Unitid = unitId
        };
        _context.Reminderunits.Add(reminderUnit);
        await _context.SaveChangesAsync();
    }
}



