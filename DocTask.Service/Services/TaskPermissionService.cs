using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Data;
using Microsoft.EntityFrameworkCore;

namespace DocTask.Service.Services;

public class TaskPermissionService : ITaskPermissionService
{
    private readonly ITaskRepository _taskRepository;
    private readonly IUnitRepository _unitRepository;
    private readonly ApplicationDbContext _context;

    public TaskPermissionService(ITaskRepository taskRepository, IUnitRepository unitRepository, ApplicationDbContext context)
    {
        _taskRepository = taskRepository;
        _unitRepository = unitRepository;
        _context = context;
    }

    public async Task<bool> CanSubmitReportAsync(int userId, int taskId)
    {
        // Người được giao được lấy từ bảng taskassignees (Task.Users)
        var task = await _context.Tasks
            .Include(t => t.Users)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);
        if (task == null) return false;
        
        // Get leader ids of assigned units

        var leaderIds = await _unitRepository.GetLeaderIdsOfAssignedUnitsByTaskId(taskId);

        return task.Users.Any(u => u.UserId == userId) || leaderIds.Any(l => l == userId);
    }

    public async Task<bool> CanViewTaskAsync(int userId, int taskId)
    {
        var task = await _context.Tasks
            .Include(t => t.Users)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);
        if (task == null) return false;
        
        var leaderIds = await _unitRepository.GetLeaderIdsOfAssignedUnitsByTaskId(taskId);
        

        // Cho phép xem nếu là người giao (assigner) hoặc nằm trong danh sách taskassignees
        return (task.AssignerId == userId) || task.Users.Any(u => u.UserId == userId) || leaderIds.Any(l => l == userId);
    }

    public async Task<bool> CanEditTaskAsync(int userId, int taskId)
    {
        var task = await _taskRepository.GetTaskByIdAsync(taskId);
        if (task == null) return false;

        // Only the assigner can edit the task
        return task.AssignerId == userId;
    }

    public async Task<bool> CanDeleteTaskAsync(int userId, int taskId)
    {
        // Chỉ người giao task có thể xóa progress thuộc task đó
        var task = await _context.Tasks.FirstOrDefaultAsync(t => t.TaskId == taskId);
        if (task == null) return false;
        return task.AssignerId == userId;
    }

    public async Task<bool> CanSubmitReportByScheduleAsync(int userId, int taskId)
    {
        var task = await _context.Tasks
            .Include(t => t.Frequency)
            .ThenInclude(f => f.FrequencyDetails)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);

        if (task == null) return false;

        // Check if user can submit report
        if (!await CanSubmitReportAsync(userId, taskId)) return false;

        // Cho phép nộp vượt tổng số yêu cầu (không chặn theo tổng số report)

        // Kiểm tra theo chu kỳ (daily/weekly/monthly) và cửa sổ thời gian hợp lệ
        var canSubmitInCurrentPeriod = await CanSubmitInCurrentPeriodAsync(userId, taskId, task);
        
        Console.WriteLine($"DEBUG: Task {taskId} - Can submit in current period: {canSubmitInCurrentPeriod}");
        
        return canSubmitInCurrentPeriod;
    }

    private async Task<bool> CanSubmitInCurrentPeriodAsync(int userId, int taskId, Core.Models.Task task)
    {
        var now = DateTime.UtcNow;

        // Chỉ giới hạn theo cửa sổ thời gian của task (không giới hạn số lần, không ràng buộc theo kỳ)
        // Cho phép nộp trong ngày due date (so sánh theo ngày, không theo giờ)
        if (task.DueDate.HasValue && now.Date > task.DueDate.Value.Date)
        {
            Console.WriteLine($"[PROGRESS-WINDOW] taskId={taskId} userId={userId} canSubmit=false (now.Date > dueDate.Date)");
            return false;
        }
        if (task.StartDate.HasValue && now.Date < task.StartDate.Value.Date)
        {
            Console.WriteLine($"[PROGRESS-WINDOW] taskId={taskId} userId={userId} canSubmit=false (now.Date < startDate.Date)");
            return false;
        }

        Console.WriteLine($"[PROGRESS-WINDOW] taskId={taskId} userId={userId} canSubmit=true (within StartDate..DueDate)");
        return true;
    }

    private int CalculateTotalRequiredReports(Core.Models.Task task)
    {
        if (task.StartDate == null || task.DueDate == null)
            return 1; // Default to 1 report if no dates

        var startDate = task.StartDate.Value;
        var dueDate = task.DueDate.Value;

        if (task.Frequency == null)
        {
            // No frequency - report only on due date
            return 1;
        }

        var frequency = task.Frequency;
        var details = frequency.FrequencyDetails.ToList();

        return frequency.FrequencyType.ToLower() switch
        {
            "daily" => CalculateDailyReports(startDate, dueDate, frequency.IntervalValue),
            "weekly" => CalculateWeeklyReports(startDate, dueDate, frequency.IntervalValue, details),
            "monthly" => CalculateMonthlyReports(startDate, dueDate, frequency.IntervalValue, details),
            _ => 1
        };
    }

    private int CalculateDailyReports(DateTime startDate, DateTime dueDate, int intervalValue)
    {
        var days = (dueDate - startDate).Days;
        return Math.Max(1, days / intervalValue + 1);
    }

    private int CalculateWeeklyReports(DateTime startDate, DateTime dueDate, int intervalValue, List<FrequencyDetail> details)
    {
        if (!details.Any())
            return 1;

        var dayOfWeeks = details.Where(d => d.DayOfWeek.HasValue).Select(d => d.DayOfWeek!.Value).ToList();
        if (!dayOfWeeks.Any())
            return 1;

        var reportDates = new List<DateTime>();
        var currentDate = startDate;

        // Find first occurrence of any specified day of week
        while (currentDate <= dueDate)
        {
            var dayOfWeek = (int)currentDate.DayOfWeek;
            if (dayOfWeeks.Contains(dayOfWeek))
            {
                reportDates.Add(currentDate);
                break;
            }
            currentDate = currentDate.AddDays(1);
        }

        if (!reportDates.Any())
            return 1;

        // Continue with interval
        var intervalDays = intervalValue * 7;
        currentDate = reportDates.First().AddDays(intervalDays);

        while (currentDate <= dueDate)
        {
            var dayOfWeek = (int)currentDate.DayOfWeek;
            if (dayOfWeeks.Contains(dayOfWeek))
            {
                reportDates.Add(currentDate);
            }
            currentDate = currentDate.AddDays(1);

            // If we've passed all days of the week, move to next interval
            if (currentDate.DayOfWeek == DayOfWeek.Sunday)
            {
                currentDate = currentDate.AddDays((intervalValue - 1) * 7);
            }
        }

        return reportDates.Count;
    }

    private int CalculateMonthlyReports(DateTime startDate, DateTime dueDate, int intervalValue, List<FrequencyDetail> details)
    {
        if (!details.Any())
            return 1;

        var dayOfMonths = details.Where(d => d.DayOfMonth.HasValue).Select(d => d.DayOfMonth!.Value).ToList();
        if (!dayOfMonths.Any())
            return 1;

        var reportDates = new List<DateTime>();
        var currentDate = startDate;

        while (currentDate <= dueDate)
        {
            foreach (var dayOfMonth in dayOfMonths)
            {
                var reportDate = new DateTime(currentDate.Year, currentDate.Month, Math.Min(dayOfMonth, DateTime.DaysInMonth(currentDate.Year, currentDate.Month)));
                if (reportDate >= startDate && reportDate <= dueDate)
                {
                    reportDates.Add(reportDate);
                }
            }

            // Move to next month
            currentDate = currentDate.AddMonths(intervalValue);
        }

        return reportDates.Distinct().Count();
    }

    private (DateTime StartDate, DateTime EndDate) GetAnchoredPeriod(DateTime anchorStart, DateTime nowUtc, string frequencyType, int intervalValue)
    {
        // Bảo toàn time-of-day, dùng UTC
        var anchor = anchorStart.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(anchorStart, DateTimeKind.Utc) : anchorStart.ToUniversalTime();
        var now = nowUtc.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc) : nowUtc.ToUniversalTime();
        if (intervalValue <= 0) intervalValue = 1;

        if (frequencyType == "daily")
        {
            var totalDays = (now - anchor).TotalDays;
            var periodsElapsed = totalDays >= 0 ? (int)Math.Floor(totalDays / intervalValue) : 0;
            var periodStart = anchor.AddDays(periodsElapsed * intervalValue);
            var periodEnd = periodStart.AddDays(intervalValue); // [start, end)
            return (periodStart, periodEnd);
        }

        if (frequencyType == "weekly")
        {
            var spanDays = 7 * intervalValue;
            var totalDays = (now - anchor).TotalDays;
            var periodsElapsed = totalDays >= 0 ? (int)Math.Floor(totalDays / spanDays) : 0;
            var periodStart = anchor.AddDays(periodsElapsed * spanDays);
            var periodEnd = periodStart.AddDays(spanDays); // [start, end)
            return (periodStart, periodEnd);
        }

        if (frequencyType == "monthly")
        {
            int monthsDiff = (now.Year - anchor.Year) * 12 + (now.Month - anchor.Month);
            var periodsElapsed = monthsDiff >= 0 ? monthsDiff / intervalValue : 0;
            // Giữ nguyên giờ/phút/giây của anchor, điều chỉnh tháng theo chu kỳ
            var baseStart = new DateTime(anchor.Year, anchor.Month, Math.Min(anchor.Day, DateTime.DaysInMonth(anchor.Year, anchor.Month)), anchor.Hour, anchor.Minute, anchor.Second, anchor.Kind);
            var periodStart = baseStart.AddMonths(periodsElapsed * intervalValue);
            // tránh tràn ngày khi cộng tháng
            periodStart = new DateTime(periodStart.Year, periodStart.Month, Math.Min(anchor.Day, DateTime.DaysInMonth(periodStart.Year, periodStart.Month)), anchor.Hour, anchor.Minute, anchor.Second, anchor.Kind);
            var periodEnd = periodStart.AddMonths(intervalValue); // [start, end)
            return (periodStart, periodEnd);
        }

        // mặc định daily
        var defTotalDays = (now - anchor).TotalDays;
        var defPeriods = defTotalDays >= 0 ? (int)Math.Floor(defTotalDays / intervalValue) : 0;
        var defStart = anchor.AddDays(defPeriods * intervalValue);
        var defEnd = defStart.AddDays(intervalValue);
        return (defStart, defEnd);
    }

    private int GetMaxReportsPerPeriod(Frequency frequency)
    {
        // Nếu IntervalValue > 0, sử dụng giá trị đó
        if (frequency.IntervalValue > 0)
        {
            Console.WriteLine($"DEBUG: Using IntervalValue: {frequency.IntervalValue}");
            return frequency.IntervalValue;
        }

        // Nếu IntervalValue = 0 hoặc null, sử dụng giá trị mặc định theo FrequencyType
        var defaultValue = frequency.FrequencyType.ToLower() switch
        {
            "daily" => 1,      // Mỗi ngày chỉ được nộp 1 báo cáo
            "weekly" => 1,     // Mỗi tuần chỉ được nộp 1 báo cáo
            "monthly" => 1,    // Mỗi tháng chỉ được nộp 1 báo cáo
            _ => 1             // Mặc định là 1
        };
        
        Console.WriteLine($"DEBUG: Using default value for {frequency.FrequencyType}: {defaultValue}");
        return defaultValue;
    }

    public async Task<List<int>> GetAuthorizedUserIdsAsync(int taskId)
    {
        var task = await _context.Tasks
            .Include(t => t.Users)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);
        if (task == null) return new List<int>();

        return task.Users.Select(u => u.UserId).Distinct().ToList();
    }

    public async Task<bool> CanAddProgressAsync(int taskId)
    {
        var task = await _taskRepository.GetTaskByIdAsync(taskId);
        if (task == null) return false;

        // Chỉ cho phép thêm tiến độ cho task con (có parentTaskId != null)
        return task.ParentTaskId.HasValue;
    }

    public async Task<bool> CanAddProgressAsync(int userId, int taskId)
    {
        var task = await _taskRepository.GetTaskByIdAsync(taskId);
        if (task == null) return false;

        // Chỉ cho phép thêm tiến độ cho task con (có parentTaskId != null)
        if (!task.ParentTaskId.HasValue) return false;

        // Kiểm tra quyền truy cập - chỉ assigner hoặc assignee mới có thể thêm tiến độ
        return await CanViewTaskAsync(userId, taskId);
    }

    // Unit hierarchy permission methods
    public async Task<bool> CanAssignTaskToUnitAsync(int userId, int unitId)
    {
        // Lấy đơn vị của user
        var userUnit = await GetUserUnitAsync(userId);
        if (userUnit == null) return false;

        // Kiểm tra quyền giao việc theo phân cấp đơn vị
        return await _unitRepository.CanAssignToUnitAsync(userUnit.UnitId, unitId);
    }

    public async Task<bool> CanViewUnitTasksAsync(int userId, int unitId)
    {
        // Lấy đơn vị của user
        var userUnit = await GetUserUnitAsync(userId);
        if (userUnit == null) return false;

        // User có thể xem task của đơn vị mình và đơn vị con
        return await _unitRepository.IsChildUnitAsync(userUnit.UnitId, unitId) ||
               userUnit.UnitId == unitId;
    }

    public async Task<bool> CanManageUnitAsync(int userId, int unitId)
    {
        // Lấy đơn vị của user
        var userUnit = await GetUserUnitAsync(userId);
        if (userUnit == null) return false;

        // User có thể quản lý đơn vị mình và đơn vị con
        return await _unitRepository.IsChildUnitAsync(userUnit.UnitId, unitId) ||
               userUnit.UnitId == unitId;
    }

    public async Task<List<int>> GetAssignableUnitIdsAsync(int userId)
    {
        // Lấy đơn vị của user
        var userUnit = await GetUserUnitAsync(userId);
        if (userUnit == null) return new List<int>();

        // Lấy danh sách đơn vị có thể giao việc
        var assignableUnits = await _unitRepository.GetAssignableUnitsAsync(userUnit.UnitId);
        return assignableUnits.Select(u => u.UnitId).ToList();
    }

    private async Task<Unit?> GetUserUnitAsync(int userId)
    {
        // Lấy đơn vị của user từ bảng Unitusers
        var userUnit = await _context.Unitusers
            .Include(uu => uu.Unit)
            .FirstOrDefaultAsync(uu => uu.UserId == userId);
        
        return userUnit?.Unit;
    }
}
