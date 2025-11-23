using DocTask.Core.DTOs.Reports;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Data;
using Microsoft.EntityFrameworkCore;

namespace DocTask.Service.Services;

/// <summary>
/// Service xử lý logic nộp báo cáo
/// </summary>
public class ReportSubmissionService : IReportSubmissionService
{
    private readonly ITaskRepository _taskRepository;
    private readonly ApplicationDbContext _context;

    public ReportSubmissionService(ITaskRepository taskRepository, ApplicationDbContext context)
    {
        _taskRepository = taskRepository;
        _context = context;
    }

    /// <summary>
    /// Kiểm tra xem user có thể nộp báo cáo cho task không
    /// </summary>
    /// <param name="userId">ID của user</param>
    /// <param name="taskId">ID của task</param>
    /// <returns>True nếu có thể nộp, False nếu không</returns>
    public async Task<ReportSubmissionResult> CanSubmitReportAsync(int userId, int taskId)
    {
        Console.WriteLine($"=== VALIDATION REPORT SUBMISSION ===");
        Console.WriteLine($"Task ID: {taskId}, User ID: {userId}");
        
        try
        {
            // Bước 1: Lấy thông tin task
            var task = await GetTaskWithFrequencyAsync(taskId);
            if (task == null)
            {
                return new ReportSubmissionResult
                {
                    CanSubmit = false,
                    Reason = "Task không tồn tại",
                    ErrorCode = "TASK_NOT_FOUND"
                };
            }

            LogTaskInfo(task);

            // Bước 2: Kiểm tra quyền nộp báo cáo
            var authResult = CheckUserAuthorization(userId, task);
            if (!authResult.CanSubmit)
            {
                return authResult;
            }

            // Bước 3: Tính toán số lượng báo cáo yêu cầu
            var totalRequiredReports = CalculateTotalRequiredReports(task);
            Console.WriteLine($"📊 Total required reports: {totalRequiredReports}");

            // Bước 4: Đếm số báo cáo đã nộp
            var totalReportsSubmitted = await CountSubmittedReportsAsync(taskId, userId);
            Console.WriteLine($"📊 Total reports submitted: {totalReportsSubmitted}");

            // Bước 5: Kiểm tra tổng số báo cáo
            if (totalReportsSubmitted >= totalRequiredReports)
            {
                return new ReportSubmissionResult
                {
                    CanSubmit = false,
                    Reason = $"Đã nộp đủ số báo cáo yêu cầu ({totalReportsSubmitted}/{totalRequiredReports})",
                    ErrorCode = "MAX_REPORTS_REACHED",
                    TotalSubmitted = totalReportsSubmitted,
                    TotalRequired = totalRequiredReports
                };
            }

            // Bước 6: Kiểm tra chu kỳ hiện tại
            var periodResult = await CheckCurrentPeriodAsync(userId, taskId, task);
            if (!periodResult.CanSubmit)
            {
                return periodResult;
            }

            Console.WriteLine($"✅ All checks passed - CAN SUBMIT REPORT");
            Console.WriteLine($"=== END VALIDATION ===\n");

            return new ReportSubmissionResult
            {
                CanSubmit = true,
                Reason = "Có thể nộp báo cáo",
                TotalSubmitted = totalReportsSubmitted,
                TotalRequired = totalRequiredReports
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error during validation: {ex.Message}");
            return new ReportSubmissionResult
            {
                CanSubmit = false,
                Reason = $"Lỗi hệ thống: {ex.Message}",
                ErrorCode = "SYSTEM_ERROR"
            };
        }
    }

    #region Private Methods

    private async Task<Core.Models.Task?> GetTaskWithFrequencyAsync(int taskId)
    {
        return await _context.Tasks
            .Include(t => t.Frequency)
            .ThenInclude(f => f.FrequencyDetails)
            .FirstOrDefaultAsync(t => t.TaskId == taskId);
    }

    private void LogTaskInfo(Core.Models.Task task)
    {
        Console.WriteLine($"✅ Task found: {task.Title}");
        Console.WriteLine($"   - AssigneeId: {task.AssigneeId}");
        Console.WriteLine($"   - StartDate: {task.StartDate}");
        Console.WriteLine($"   - DueDate: {task.DueDate}");
        Console.WriteLine($"   - Frequency: {(task.Frequency != null ? $"{task.Frequency.FrequencyType} (Interval: {task.Frequency.IntervalValue})" : "None")}");
    }

    private ReportSubmissionResult CheckUserAuthorization(int userId, Core.Models.Task task)
    {
        if (task.AssigneeId != userId)
        {
            return new ReportSubmissionResult
            {
                CanSubmit = false,
                Reason = "Chỉ người được giao việc mới có thể nộp báo cáo",
                ErrorCode = "UNAUTHORIZED"
            };
        }

        Console.WriteLine($"✅ User {userId} is authorized to submit reports");
        return new ReportSubmissionResult { CanSubmit = true };
    }

    private async Task<int> CountSubmittedReportsAsync(int taskId, int userId)
    {
        return await _context.Progresses
            .CountAsync(p => p.TaskId == taskId && p.UpdatedBy == userId);
    }

    private int CalculateTotalRequiredReports(Core.Models.Task task)
    {
        if (task.StartDate == null || task.DueDate == null)
        {
            Console.WriteLine($"⚠️ Task has no start/due date - defaulting to 1 report");
            return 1;
        }

        var startDate = task.StartDate.Value;
        var dueDate = task.DueDate.Value;

        if (task.Frequency == null)
        {
            // Không có frequency - tính theo số ngày
            var days = (dueDate - startDate).Days + 1;
            var reports = Math.Max(1, days);
            Console.WriteLine($"📅 No frequency - calculating by days: {days} days = {reports} reports");
            return reports;
        }

        var frequency = task.Frequency;
        var details = frequency.FrequencyDetails.ToList();

        var result = frequency.FrequencyType.ToLower() switch
        {
            "daily" => CalculateDailyReports(startDate, dueDate, frequency.IntervalValue),
            "weekly" => CalculateWeeklyReports(startDate, dueDate, frequency.IntervalValue, details),
            "monthly" => CalculateMonthlyReports(startDate, dueDate, frequency.IntervalValue, details),
            _ => 1
        };

        Console.WriteLine($"📊 Calculated required reports for {frequency.FrequencyType}: {result}");
        return result;
    }

    private int CalculateDailyReports(DateTime startDate, DateTime dueDate, int intervalValue)
    {
        if (intervalValue <= 0) intervalValue = 1;
        
        var days = (dueDate - startDate).Days + 1;
        var reports = (int)Math.Ceiling((double)days / intervalValue);
        
        Console.WriteLine($"📅 Daily calculation: {days} days / {intervalValue} = {reports} reports");
        return Math.Max(1, reports);
    }

    private int CalculateWeeklyReports(DateTime startDate, DateTime dueDate, int intervalValue, List<FrequencyDetail> details)
    {
        if (!details.Any())
        {
            Console.WriteLine($"⚠️ No frequency details for weekly - defaulting to 1");
            return 1;
        }

        var dayOfWeeks = details.Where(d => d.DayOfWeek.HasValue).Select(d => d.DayOfWeek!.Value).ToList();
        if (!dayOfWeeks.Any())
        {
            Console.WriteLine($"⚠️ No valid day of week - defaulting to 1");
            return 1;
        }

        var reportDates = new List<DateTime>();
        var currentDate = startDate;

        // Tìm ngày đầu tiên phù hợp
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
        {
            Console.WriteLine($"⚠️ No valid report dates found - defaulting to 1");
            return 1;
        }

        // Tiếp tục với interval
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

            // Nếu đã qua hết tuần, chuyển sang interval tiếp theo
            if (currentDate.DayOfWeek == DayOfWeek.Sunday)
            {
                currentDate = currentDate.AddDays((intervalValue - 1) * 7);
            }
        }

        Console.WriteLine($"📅 Weekly calculation: {reportDates.Count} reports on days: {string.Join(", ", reportDates.Select(d => d.ToString("dd/MM")))}");
        return reportDates.Count;
    }

    private int CalculateMonthlyReports(DateTime startDate, DateTime dueDate, int intervalValue, List<FrequencyDetail> details)
    {
        if (!details.Any())
        {
            Console.WriteLine($"⚠️ No frequency details for monthly - defaulting to 1");
            return 1;
        }

        var dayOfMonths = details.Where(d => d.DayOfMonth.HasValue).Select(d => d.DayOfMonth!.Value).ToList();
        if (!dayOfMonths.Any())
        {
            Console.WriteLine($"⚠️ No valid day of month - defaulting to 1");
            return 1;
        }

        var reportDates = new List<DateTime>();
        var currentDate = startDate;

        while (currentDate <= dueDate)
        {
            foreach (var dayOfMonth in dayOfMonths)
            {
                var reportDate = new DateTime(currentDate.Year, currentDate.Month, 
                    Math.Min(dayOfMonth, DateTime.DaysInMonth(currentDate.Year, currentDate.Month)));
                
                if (reportDate >= startDate && reportDate <= dueDate)
                {
                    reportDates.Add(reportDate);
                }
            }

            // Chuyển sang tháng tiếp theo
            currentDate = currentDate.AddMonths(intervalValue);
        }

        var distinctReports = reportDates.Distinct().Count();
        Console.WriteLine($"📅 Monthly calculation: {distinctReports} reports on days: {string.Join(", ", reportDates.Distinct().Select(d => d.ToString("dd/MM")))}");
        return distinctReports;
    }

    private async Task<ReportSubmissionResult> CheckCurrentPeriodAsync(int userId, int taskId, Core.Models.Task task)
    {
        // Nếu không có frequency, mặc định là daily
        if (task.Frequency == null)
        {
            return await CheckDailyPeriodAsync(userId, taskId, "No frequency - treating as daily");
        }

        var frequency = task.Frequency;
        var currentPeriod = GetCurrentPeriod(frequency);
        
        if (currentPeriod == null)
        {
            Console.WriteLine($"⚠️ Cannot determine current period - allowing submission");
            return new ReportSubmissionResult { CanSubmit = true };
        }

        var reportsInCurrentPeriod = await _context.Progresses
            .CountAsync(p => p.TaskId == taskId 
                        && p.UpdatedBy == userId 
                        && p.UpdatedAt >= currentPeriod.Value.StartDate 
                        && p.UpdatedAt <= currentPeriod.Value.EndDate);

        var maxReportsPerPeriod = GetMaxReportsPerPeriod(frequency);

        Console.WriteLine($"📊 Current period: {currentPeriod.Value.StartDate:yyyy-MM-dd HH:mm:ss} to {currentPeriod.Value.EndDate:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"📊 Reports in current period: {reportsInCurrentPeriod}, Max allowed: {maxReportsPerPeriod}");

        if (reportsInCurrentPeriod >= maxReportsPerPeriod)
        {
            return new ReportSubmissionResult
            {
                CanSubmit = false,
                Reason = $"Đã nộp đủ số báo cáo trong chu kỳ hiện tại ({reportsInCurrentPeriod}/{maxReportsPerPeriod})",
                ErrorCode = "PERIOD_LIMIT_REACHED"
            };
        }

        return new ReportSubmissionResult { CanSubmit = true };
    }

    private async Task<ReportSubmissionResult> CheckDailyPeriodAsync(int userId, int taskId, string reason)
    {
        Console.WriteLine($"📅 {reason}");
        
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        var reportsToday = await _context.Progresses
            .CountAsync(p => p.TaskId == taskId 
                        && p.UpdatedBy == userId 
                        && p.UpdatedAt >= today 
                        && p.UpdatedAt < tomorrow);

        Console.WriteLine($"📊 Reports today: {reportsToday}, Max allowed: 1");

        if (reportsToday >= 1)
        {
            return new ReportSubmissionResult
            {
                CanSubmit = false,
                Reason = "Đã nộp báo cáo trong ngày hôm nay",
                ErrorCode = "DAILY_LIMIT_REACHED"
            };
        }

        return new ReportSubmissionResult { CanSubmit = true };
    }

    private (DateTime StartDate, DateTime EndDate)? GetCurrentPeriod(Frequency frequency)
    {
        var now = DateTime.UtcNow;
        
        return frequency.FrequencyType.ToLower() switch
        {
            "daily" => (now.Date, now.Date.AddDays(1).AddTicks(-1)),
            "weekly" => GetWeeklyPeriod(now),
            "monthly" => GetMonthlyPeriod(now),
            _ => null
        };
    }

    private (DateTime StartDate, DateTime EndDate) GetWeeklyPeriod(DateTime now)
    {
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);
        return (startOfWeek, endOfWeek);
    }

    private (DateTime StartDate, DateTime EndDate) GetMonthlyPeriod(DateTime now)
    {
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1).AddTicks(-1);
        return (startOfMonth, endOfMonth);
    }

    private int GetMaxReportsPerPeriod(Frequency frequency)
    {
        if (frequency.IntervalValue > 0)
        {
            return frequency.IntervalValue;
        }

        return frequency.FrequencyType.ToLower() switch
        {
            "daily" => 1,
            "weekly" => 1,
            "monthly" => 1,
            _ => 1
        };
    }

    #endregion
}

