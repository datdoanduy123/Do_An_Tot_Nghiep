using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Data;
using Microsoft.EntityFrameworkCore;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Service.Services;

public class ProgressCalculationService : IProgressCalculationService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ApplicationDbContext _context;

    public ProgressCalculationService(ITaskRepository taskRepository, ApplicationDbContext context)
    {
        _taskRepository = taskRepository;
        _context = context;
    }

    public async Task<ProgressCalculationResponse> CalculateTaskProgressAsync(int taskId)
    {
        var task = await _context.Tasks
            .Include(t => t.Frequency)
            .ThenInclude(f => f.FrequencyDetails)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);

        if (task == null)
            throw new KeyNotFoundException($"Task with ID {taskId} not found.");

        var response = new ProgressCalculationResponse
        {
            TaskId = task.TaskId,
            Title = task.Title,
            IsParentTask = task.ParentTaskId == null
        };

        if (task.ParentTaskId == null)
        {
        
            var children = await _context.Tasks
                .Where(t => t.ParentTaskId == taskId)
                .Include(t => t.Frequency)
                .ThenInclude(f => f.FrequencyDetails)
                .ToListAsync();

            var childProgresses = new List<ChildTaskProgress>();
            double totalProgress = 0;
            int childCount = 0;

            foreach (var child in children)
            {
                var (totalPeriods, periodsWithReports) = await CalculateMultiUserTaskProgressAsync(child);
                var childPercentage = totalPeriods > 0 ? (double)periodsWithReports / totalPeriods * 100 : 0;
                childProgresses.Add(new ChildTaskProgress
                {
                    TaskId = child.TaskId,
                    Title = child.Title,
                    TotalProgressRecords = periodsWithReports,
                    TotalRequiredReports = totalPeriods,
                    ProgressPercentage = childPercentage
                });
                totalProgress += childPercentage;
                childCount++;
            }

            response.ChildTasks = childProgresses;
            response.ProgressPercentage = childCount > 0 ? totalProgress / childCount : 0;
            response.TotalProgressRecords = childProgresses.Sum(c => c.TotalProgressRecords);
            response.TotalRequiredReports = childProgresses.Sum(c => c.TotalRequiredReports);

            // Cập nhật phần trăm vào parent task
            var oldParentPercentage = task.Percentagecomplete;
            task.Percentagecomplete = (int)Math.Round(response.ProgressPercentage);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"[PARENT-PERCENTAGE-UPDATE] Parent Task {task.TaskId}: {oldParentPercentage}% -> {task.Percentagecomplete}% (change: {task.Percentagecomplete - oldParentPercentage}%)");
        }
        else
        {
            // Child task - percentage is ratio of periods having at least one report
            // Sử dụng logic mới cho multi-user tasks
            var (totalPeriods, periodsWithReports) = await CalculateMultiUserTaskProgressAsync(task);
            response.TotalProgressRecords = periodsWithReports; // informational
            response.TotalRequiredReports = totalPeriods;
            response.ProgressPercentage = totalPeriods > 0 ? (double)periodsWithReports / totalPeriods * 100 : 0;
            
            Console.WriteLine($"[CHILD-TASK-DEBUG] Task {task.TaskId}: {periodsWithReports}/{totalPeriods} = {response.ProgressPercentage:F2}%");

            // Cập nhật phần trăm vào child task
            var oldPercentage = task.Percentagecomplete;
            task.Percentagecomplete = (int)Math.Round(response.ProgressPercentage);
            await _context.SaveChangesAsync();
            
            Console.WriteLine($"[PERCENTAGE-UPDATE] Task {task.TaskId}: {oldPercentage}% -> {task.Percentagecomplete}% (change: {task.Percentagecomplete - oldPercentage}%)");

            // Đồng thời cập nhật phần trăm cho task cha (nếu có)
            if (task.ParentTaskId.HasValue)
            {
                var parent = await _context.Tasks
                    .Include(t => t.Frequency)
                    .ThenInclude(f => f.FrequencyDetails)
                    .FirstOrDefaultAsync(t => t.TaskId == task.ParentTaskId.Value);
                if (parent != null)
                {
                    // Tính trung bình các con
                    var children = await _context.Tasks
                        .Where(t => t.ParentTaskId == parent.TaskId)
                        .Include(t => t.Frequency)
                        .ThenInclude(f => f.FrequencyDetails)
                        .ToListAsync();
                    double total = 0;
                    int count = 0;
                    foreach (var child in children)
                    {
                        var (childTotal, childDone) = await CalculateMultiUserTaskProgressAsync(child);
                        var pct = childTotal > 0 ? (double)childDone / childTotal * 100 : 0;
                        total += pct;
                        count++;
                    }
                    var oldParentPercentage = parent.Percentagecomplete;
                    parent.Percentagecomplete = count > 0 ? (int)Math.Round(total / count) : 0;
                    await _context.SaveChangesAsync();
                    
                    Console.WriteLine($"[PARENT-PERCENTAGE-UPDATE] Parent Task {parent.TaskId}: {oldParentPercentage}% -> {parent.Percentagecomplete}% (change: {parent.Percentagecomplete - oldParentPercentage}%)");
                }
            }
        }

        return response;
    }

    private async Task<(int totalPeriods, int periodsWithReports)> CalculateChildPeriodsCompletionAsync(TaskModel task)
    {
        Console.WriteLine($"[CALC-DEBUG] CalculateChildPeriodsCompletionAsync called for task {task.TaskId}");
        
        if (task.StartDate == null || task.DueDate == null)
        {
            Console.WriteLine($"[CALC-DEBUG] Task {task.TaskId} has no start/due date");
            return (0, 0);
        }

        var start = task.StartDate.Value.Date;
        var due = task.DueDate.Value.Date;
        var freqType = task.Frequency?.FrequencyType?.Trim().ToLower() ?? "daily";
        var interval = task.Frequency?.IntervalValue > 0 ? task.Frequency!.IntervalValue : 1;
        
        Console.WriteLine($"[CALC-DEBUG] Task {task.TaskId}: start={start:yyyy-MM-dd}, due={due:yyyy-MM-dd}, freqType='{freqType}', interval={interval}");

        // Generate period windows
        var windows = new List<(DateTime s, DateTime e)>();
        
        if (freqType == "weekly")
        {
            Console.WriteLine($"[WEEKLY-DEBUG] Task {task.TaskId}: start={start:yyyy-MM-dd}, due={due:yyyy-MM-dd}, interval={interval}");
            
            // Sử dụng day từ database (1=Chủ nhật, 2=Thứ 2, ..., 7=Thứ 7)
            var weeklyDays = task.Frequency?.FrequencyDetails?.Select(fd => fd.DayOfWeek).Where(d => d.HasValue).Select(d => d.Value).ToList() ?? new List<int>();
            var targetDays = NormalizeWeeklyDays(weeklyDays);
            Console.WriteLine($"[WEEKLY-DEBUG] Weekly days from DB: {string.Join(",", weeklyDays)}");
            Console.WriteLine($"[WEEKLY-DEBUG] Normalized days: {string.Join(",", targetDays)}");
            
            if (targetDays.Count == 0)
            {
                Console.WriteLine($"[WEEKLY-DEBUG] No valid days specified, using Sunday as default");
                targetDays.Add(DayOfWeek.Sunday);
            }
            
            // Tìm ngày báo cáo đầu tiên từ startDate
            var firstReportDay = FindNextReportDay(start, targetDays);
            var firstStart = start;
            var firstEnd = firstReportDay <= due ? firstReportDay : due;
            windows.Add((firstStart, firstEnd));
            Console.WriteLine($"[WEEKLY-DEBUG] Period 1: {firstStart:yyyy-MM-dd} to {firstEnd:yyyy-MM-dd} (first report period)");

            // Các kỳ tiếp theo: mỗi kỳ cách nhau intervalValue tuần, kết thúc vào ngày báo cáo
            var spanDays = 7 * interval;
            var lastEnd = firstReportDay;
            int weeklyPeriodCount = 1;
            while (true)
            {
                var nextEnd = lastEnd.AddDays(spanDays);
                var nextStart = lastEnd.AddDays(1); // kỳ tiếp theo bắt đầu từ ngày sau kỳ trước
                
                // Chỉ tạo kỳ mới nếu có đủ thời gian cho ít nhất 1 ngày báo cáo
                if (nextStart > due) break;
                
                // Tìm ngày báo cáo gần nhất trong kỳ này
                var reportDay = FindNextReportDay(nextStart, targetDays);
                if (reportDay > due)
                {
                    Console.WriteLine($"[WEEKLY-DEBUG] Skipping period {weeklyPeriodCount + 1}: no report day in final period");
                    break;
                }
                
                var endDate = reportDay;
                
                // Chỉ thêm kỳ nếu có ít nhất 1 ngày (tránh kỳ rỗng)
                if (nextStart <= endDate)
                {
                    windows.Add((nextStart, endDate));
                    weeklyPeriodCount++;
                    Console.WriteLine($"[WEEKLY-DEBUG] Period {weeklyPeriodCount}: {nextStart:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                }
                lastEnd = nextEnd;
            }
            Console.WriteLine($"[WEEKLY-DEBUG] Total periods generated: {windows.Count}");
        }
        else
        {
            var tempday = start;
            while (tempday <= due)
            {
                DateTime end = freqType switch
                {
                    "daily" => tempday.AddDays(interval - 1),
                    "monthly" => tempday.AddMonths(interval).AddDays(-1),
                    _ => tempday.AddDays(interval - 1)
                };
                if (end > due) end = due;
                windows.Add((tempday, end));
                tempday = freqType switch
                {
                    "daily" => tempday.AddDays(interval),
                    "monthly" => tempday.AddMonths(interval),
                    _ => tempday.AddDays(interval)
                };
            }
        }

        if (windows.Count == 0) return (0, 0);

        // Lấy tất cả người được giao việc
        var assignedUsers = await _context.Tasks
            .Where(t => t.TaskId == task.TaskId)
            .SelectMany(t => t.Users)
            .Select(u => u.UserId)
            .Distinct()
            .ToListAsync();
        
        Console.WriteLine($"[CALC-DEBUG] Task {task.TaskId} has {assignedUsers.Count} assigned users: {string.Join(",", assignedUsers)}");

        // Tính tổng số báo cáo yêu cầu của TẤT CẢ người được giao việc
        var totalRequiredReports = windows.Count * assignedUsers.Count;
        Console.WriteLine($"[CALC-DEBUG] Total required reports: {windows.Count} periods × {assignedUsers.Count} users = {totalRequiredReports}");

        // Đếm tổng số báo cáo đã được duyệt của TẤT CẢ người được giao việc
        var totalCompletedReports = await _context.Progresses
            .Where(p => p.TaskId == task.TaskId && 
                       p.Status == "completed" &&
                       p.UpdatedBy.HasValue &&
                       assignedUsers.Contains(p.UpdatedBy.Value))
            .CountAsync();
        
        Console.WriteLine($"[CALC-DEBUG] Total completed reports: {totalCompletedReports}/{totalRequiredReports}");
        
        Console.WriteLine($"[CALC-DEBUG] Task {task.TaskId}: {totalCompletedReports}/{totalRequiredReports} total reports completed by all users");
        Console.WriteLine($"[CALC-DEBUG] Progress calculation: {totalCompletedReports}/{totalRequiredReports} = {(totalRequiredReports > 0 ? (double)totalCompletedReports / totalRequiredReports * 100 : 0):F2}%");
        return (totalRequiredReports, totalCompletedReports);
    }

    /// <summary>
    /// Tính toán tiến độ cho task có nhiều người được giao việc
    /// </summary>
    private async Task<(int totalPeriods, int periodsWithReports)> CalculateMultiUserTaskProgressAsync(TaskModel task)
    {
        Console.WriteLine($"[MULTI-USER-DEBUG] CalculateMultiUserTaskProgressAsync called for task {task.TaskId}");
        
        if (task.StartDate == null || task.DueDate == null)
        {
            Console.WriteLine($"[MULTI-USER-DEBUG] Task {task.TaskId} has no start/due date");
            return (0, 0);
        }

        var start = task.StartDate.Value.Date;
        var due = task.DueDate.Value.Date;
        var freqType = task.Frequency?.FrequencyType?.Trim().ToLower() ?? "daily";
        var interval = task.Frequency?.IntervalValue > 0 ? task.Frequency!.IntervalValue : 1;
        
        Console.WriteLine($"[MULTI-USER-DEBUG] Task {task.TaskId}: start={start:yyyy-MM-dd}, due={due:yyyy-MM-dd}, freqType='{freqType}', interval={interval}");

        // Generate period windows (same logic as before)
        var windows = new List<(DateTime s, DateTime e)>();
        
        if (freqType == "weekly")
        {
            var weeklyDays = task.Frequency?.FrequencyDetails?.Select(fd => fd.DayOfWeek).Where(d => d.HasValue).Select(d => d.Value).ToList() ?? new List<int>();
            var targetDays = NormalizeWeeklyDays(weeklyDays);
            
            if (targetDays.Count == 0)
            {
                targetDays.Add(DayOfWeek.Sunday);
            }
            
            var firstReportDay = FindNextReportDay(start, targetDays);
            var firstStart = start;
            var firstEnd = firstReportDay <= due ? firstReportDay : due;
            windows.Add((firstStart, firstEnd));

            var spanDays = 7 * interval;
            var lastEnd = firstReportDay;
            int weeklyPeriodCount = 1;
            while (true)
            {
                var nextEnd = lastEnd.AddDays(spanDays);
                var nextStart = lastEnd.AddDays(1);
                
                if (nextStart > due) break;
                
                var reportDay = FindNextReportDay(nextStart, targetDays);
                if (reportDay > due) break;
                
                var endDate = reportDay;
                
                if (nextStart <= endDate)
                {
                    windows.Add((nextStart, endDate));
                    weeklyPeriodCount++;
                }
                lastEnd = nextEnd;
            }
        }
        else
        {
            var tempday = start;
            while (tempday <= due)
            {
                DateTime end = freqType switch
                {
                    "daily" => tempday.AddDays(interval - 1),
                    "monthly" => tempday.AddMonths(interval).AddDays(-1),
                    _ => tempday.AddDays(interval - 1)
                };
                if (end > due) end = due;
                windows.Add((tempday, end));
                tempday = freqType switch
                {
                    "daily" => tempday.AddDays(interval),
                    "monthly" => tempday.AddMonths(interval),
                    _ => tempday.AddDays(interval)
                };
            }
        }

        if (windows.Count == 0) return (0, 0);

        // Lấy tất cả người được giao việc
        var assignedUsers = await _context.Tasks
            .Where(t => t.TaskId == task.TaskId)
            .SelectMany(t => t.Users)
            .Select(u => u.UserId)
            .Distinct()
            .ToListAsync();
        
        Console.WriteLine($"[MULTI-USER-DEBUG] Task {task.TaskId} has {assignedUsers.Count} assigned users: {string.Join(",", assignedUsers)}");

        // Tính tổng số báo cáo yêu cầu của TẤT CẢ người được giao việc
        var totalRequiredReports = windows.Count * assignedUsers.Count;
        Console.WriteLine($"[MULTI-USER-DEBUG] Total required reports: {windows.Count} periods × {assignedUsers.Count} users = {totalRequiredReports}");

        // Đếm tổng số báo cáo đã được duyệt của TẤT CẢ người được giao việc
        var totalCompletedReports = await _context.Progresses
            .Where(p => p.TaskId == task.TaskId && 
                       p.Status == "completed" &&
                       p.UpdatedBy.HasValue &&
                       assignedUsers.Contains(p.UpdatedBy.Value))
            .CountAsync();
        
        Console.WriteLine($"[MULTI-USER-DEBUG] Total completed reports: {totalCompletedReports}/{totalRequiredReports}");
        
        Console.WriteLine($"[MULTI-USER-DEBUG] Task {task.TaskId}: {totalCompletedReports}/{totalRequiredReports} total reports completed by all users");
        Console.WriteLine($"[MULTI-USER-DEBUG] Progress calculation: {totalCompletedReports}/{totalRequiredReports} = {(totalRequiredReports > 0 ? (double)totalCompletedReports / totalRequiredReports * 100 : 0):F2}%");
        return (totalRequiredReports, totalCompletedReports);
    }

    private int CalculateRequiredReports(TaskModel task)
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

    

    private DateTime FindNextReportDay(DateTime startDate, List<DayOfWeek> targetDays)
    {
        // Tìm ngày báo cáo gần nhất từ startDate trong danh sách targetDays
        var current = startDate.Date;
        
        // Kiểm tra trong 7 ngày tới
        for (int i = 0; i < 7; i++)
        {
            if (targetDays.Contains(current.DayOfWeek))
            {
                Console.WriteLine($"[FIND-REPORT-DAY] Found report day: {current:yyyy-MM-dd} ({current.DayOfWeek})");
                return current;
            }
            current = current.AddDays(1);
        }
        
        // Nếu không tìm thấy, trả về ngày cuối cùng
        Console.WriteLine($"[FIND-REPORT-DAY] No report day found, using last day: {current.AddDays(-1):yyyy-MM-dd}");
        return current.AddDays(-1);
    }

    private List<DayOfWeek> NormalizeWeeklyDays(List<int> weeklyDaysRaw)
    {
        // Chuyển đổi từ quy ước người dùng (1=Chủ nhật, 2=Thứ 2, ..., 7=Thứ 7) sang .NET DayOfWeek (0=Chủ nhật, 1=Thứ 2, ..., 6=Thứ 7)
        var normalizedDays = new List<DayOfWeek>();
        
        foreach (var day in weeklyDaysRaw)
        {
            // Chuyển đổi: 1->0 (Sunday), 2->1 (Monday), ..., 7->6 (Saturday)
            if (day >= 1 && day <= 7)
            {
                var dotnetDay = (DayOfWeek)((day - 1) % 7);
                normalizedDays.Add(dotnetDay);
            }
        }
        
        return normalizedDays;
    }


}
