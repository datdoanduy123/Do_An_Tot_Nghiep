using DocTask.Core.Dtos.Progress;
using DocTask.Core.Dtos.Tasks;
using DocTask.Core.Dtos.UploadFile;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Core.Paginations;
using DocTask.Service.Mappers;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading;

namespace DocTask.Service.Services;

public class ProgressService : IProgressService
{
    private readonly IProgressRepository _progressRepository;
    private readonly IUploadFileService _uploadFileService;
    private readonly ITaskPermissionService _taskPermissionService;
    private readonly ITaskRepository _taskRepository;
    private readonly IUserRepository _userRepository;
    private readonly IReminderService _reminderService;
    private readonly IProgressCalculationService _progressCalculationService;
    private readonly IReminderRepository _reminderRepository;
    private readonly IUnitRepository _unitRepository;
    private readonly ISubTaskRepository _subTaskRepository;

    public ProgressService(IProgressRepository progressRepository, IUploadFileService uploadFileService, ITaskPermissionService taskPermissionService, ITaskRepository taskRepository, IUserRepository userRepository, IReminderService reminderService, IProgressCalculationService progressCalculationService, IReminderRepository reminderRepository, IUnitRepository unitRepository, ISubTaskRepository subTaskRepository)
    {
        _progressRepository = progressRepository;
        _uploadFileService = uploadFileService;
        _taskPermissionService = taskPermissionService;
        _taskRepository = taskRepository;
        _userRepository = userRepository;
        _reminderService = reminderService;
        _progressCalculationService = progressCalculationService;
        _reminderRepository = reminderRepository;
        _unitRepository = unitRepository;
        _subTaskRepository = subTaskRepository;
    }

    public async Task<UpdateProgressResponse> UpdateProgressAsync(int taskId, UpdateProgressRequest request, int? updatedBy = null)
    {
        // 1️⃣ Kiểm tra xem user đã có báo cáo pending cho kỳ này chưa
        if (request.PeriodIndex.HasValue)
        {
            var existingProgresses = await _progressRepository.GetProgressesForReviewAsync(
                taskId, null, null, null, updatedBy
            );

            var existingInPeriod = existingProgresses
                .Where(p => p.PeriodIndex == request.PeriodIndex.Value)
                .ToList();

            if (existingInPeriod.Any())
            {
                var latestProgress = existingInPeriod.OrderByDescending(p => p.UpdatedAt).First();

                // Nếu có báo cáo đã được duyệt (completed/late), KHÔNG cho nộp lại
                if (latestProgress.Status == "completed" || latestProgress.Status == "late")
                {
                    throw new InvalidOperationException(
                        $"Không thể nộp báo cáo cho kỳ {request.PeriodIndex.Value}. " +
                        $"Báo cáo đã được duyệt với trạng thái '{latestProgress.Status}'."
                    );
                }

                // Nếu có báo cáo pending, phải xóa trước
                if (latestProgress.Status == "pending")
                {
                    throw new InvalidOperationException(
                        $"Đã có báo cáo đang chờ duyệt cho kỳ {request.PeriodIndex.Value}. " +
                        $"Vui lòng xóa báo cáo cũ (ID: {latestProgress.ProgressId}) trước khi nộp báo cáo mới."
                    );
                }
            }
        }

        // 2️⃣ Upload file nếu có
        if (request.ReportFileStream != null &&
            !string.IsNullOrWhiteSpace(request.ReportFileName) &&
            request.SubmittedByUserId > 0)
        {
            var formFile = new FormFile(request.ReportFileStream, 0, request.ReportFileStream.Length, "file", request.ReportFileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };

            var uploadDto = await _uploadFileService.UploadFileAsync(
                new UploadFileRequest { File = formFile },
                request.SubmittedByUserId
            );

            request.ReportFilePath = uploadDto.FilePath;
        }

        // 3️⃣ Lấy thông tin người cập nhật
        string? fullName = null;
        if (updatedBy.HasValue)
        {
            var user = await _userRepository.GetByIdAsync(updatedBy.Value);
            fullName = user?.FullName ?? updatedBy.ToString();
        }

        // 4️⃣ Lấy thông tin task
        var task = await _taskRepository.GetTaskByIdAsync(taskId);
        var parentTaskId = task?.ParentTaskId;
        if (task == null)
        {
            throw new KeyNotFoundException("Task không tồn tại.");
        }

        var now = DateTime.UtcNow;

        // 5️⃣ Lưu thông tin thời gian nộp để dùng khi duyệt
        // Trạng thái ban đầu luôn là "pending"
        request.Status = "pending";

        // Lưu thông tin để tính status khi duyệt
        if (request.PeriodIndex.HasValue)
        {
            var frequencyType = task.Frequency?.FrequencyType?.ToLower() ?? "daily";
            var intervalValue = task.Frequency?.IntervalValue ?? 1;
            var weeklyDaysRaw = task.Frequency?.FrequencyDetails
                ?.Where(d => d.DayOfWeek.HasValue)
                .Select(d => d.DayOfWeek!.Value)
                .ToList() ?? new List<int>();

            var startDate = (task.StartDate ?? DateTime.UtcNow).Date;
            var dueDate = (task.DueDate ?? startDate).Date;

            var allPeriods = GenerateScheduledPeriods(startDate, dueDate, frequencyType, intervalValue, weeklyDaysRaw);

            if (request.PeriodIndex.Value > 0 && request.PeriodIndex.Value <= allPeriods.Count)
            {
                // Lưu thông tin này vào Comment để dùng sau
                var targetPeriod = allPeriods[request.PeriodIndex.Value - 1];
                request.Comment = $"PeriodEnd:{targetPeriod.EndDate:yyyy-MM-dd}|PeriodStart:{targetPeriod.StartDate:yyyy-MM-dd}";
            }
            else
            {
                throw new ArgumentException("PeriodIndex không hợp lệ.");
            }
        }

        // 6️⃣ Tạo mới progress
        var progress = await _progressRepository.CreateProgressAsync(taskId, request, updatedBy);

        // 7️⃣ Gửi thông báo
        var unitName = await _unitRepository.GetUserUnitNameByIdAsync(updatedBy!.Value);

        if (task != null)
        {
            var assignerId = task.AssignerId!.Value;

            var reminder = await _reminderService.CreateReminderAsync(
                taskId,
                updatedBy ?? assignerId,
                assignerId,
                $"Nhân viên {fullName} vừa cập tiến độ cho: {task.Title}"
            );

            await _reminderService.SendRealTimeNotificationAsync(
                userId: assignerId,
                title: "Tiến độ mới được cập nhật",
                message: $"Nhân viên {fullName} từ đơn vị {unitName} vừa cập nhật tiến độ cho {task.Title}.",
                data: new
                {
                    taskId,
                    parentTaskId,
                    task.Title,
                    task.Description,
                    task.StartDate,
                    task.DueDate,
                    task.Percentagecomplete,
                    task.Status,
                    IsRead = reminder.IsRead
                }
            );
        }

        // 8️⃣ Trả về kết quả
        return new UpdateProgressResponse
        {
            ProgressId = progress.ProgressId,
            TaskId = progress.TaskId,
            Proposal = progress.Proposal,
            Result = progress.Result,
            Feedback = progress.Feedback,
            Comment = progress.Comment,
            Status = progress.Status,
            FileName = progress.FileName,
            FilePath = progress.FilePath,
            UpdatedAt = progress.UpdatedAt,
            UpdatedBy = progress.UpdatedBy,
        };
    }


    public Task<List<ProgressDto>> GetProgressesByTaskAsync(int taskId)
        => _progressRepository.GetProgressesByTaskAsync(taskId);

    public Task<Core.Models.Progress?> GetProgressByIdAsync(int progressId)
        => _progressRepository.GetProgressByIdAsync(progressId);

    public async Task<Core.Models.Progress?> UpdateProgressEntryAsync(int progressId, UpdateProgressRequest request, int? updatedBy = null)
    {
        // If file stream present, upload to cloud and set ReportFilePath
        if (request.ReportFileStream != null && !string.IsNullOrWhiteSpace(request.ReportFileName) && request.SubmittedByUserId > 0)
        {
            var formFile = new FormFile(request.ReportFileStream, 0, request.ReportFileStream.Length, "file", request.ReportFileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };
            var uploadDto = await _uploadFileService.UploadFileAsync(new UploadFileRequest { File = formFile }, request.SubmittedByUserId);
            request.ReportFilePath = uploadDto.FilePath;
        }

        return await _progressRepository.UpdateProgressAsync(progressId, request, updatedBy);
    }

    public async Task<bool> DeleteProgressAsync(int progressId)
    {
        // Lấy progress để kiểm tra file path trước khi xóa
        var progress = await _progressRepository.GetProgressByIdAsync(progressId);
        if (progress == null)
            return false;

        if(progress.Status == "completed" || progress.Status == "late")
        {
            throw new InvalidOperationException("Báo cáo đã nộp không thể xóa được");
        }

        // Xóa file nếu có file path (sử dụng service có sẵn)
        if (!string.IsNullOrEmpty(progress.FilePath))
        {
            // Tìm file trong database để lấy fileId
            var files = await _uploadFileService.GetFileByUserIdAsync(progress.UpdatedBy ?? 0);
            var fileToDelete = files?.FirstOrDefault(f => f.FilePath == progress.FilePath);
            if (fileToDelete != null)
            {
                await _uploadFileService.DeleteFileAsync(fileToDelete.FileId, progress.UpdatedBy ?? 0);
            }
        }

        return await _progressRepository.DeleteProgressAsync(progressId);
    }

    public async Task<List<ProgressReviewByUserDto>> ReviewProgressByUserAsync(int taskId, DateTime? from, DateTime? to, string? status)
    {
        var records = await _progressRepository.GetProgressesForReviewAsync(taskId, from, to, status, null);
        // Lấy thông tin task chuẩn theo taskId (kể cả khi chưa có report)
        var taskModel = await _taskRepository.GetTaskByIdAsync(taskId);
        if (taskModel == null) return new List<ProgressReviewByUserDto>();
        var frequencyType = taskModel.Frequency?.FrequencyType?.Trim().ToLower() ?? "daily";

        // Nhóm theo user (chỉ các user có report)
        var userGroups = records
            .Where(p => p.UpdatedBy.HasValue)
            .GroupBy(p => p.UpdatedBy!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Lấy tất cả user được phân công (bao gồm cả người chưa có report)
        var authorizedUserIds = await _taskPermissionService.GetAuthorizedUserIdsAsync(taskId);

        var result = new List<ProgressReviewByUserDto>();
        var userNameCache = new Dictionary<int, string>();

        foreach (var userId in authorizedUserIds)
        {
            userGroups.TryGetValue(userId, out var userReports);
            var user = userReports?.FirstOrDefault()?.UpdatedByNavigation;
            string userFullName;
            if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
            {
                userFullName = user.FullName;
            }
            else if (userNameCache.TryGetValue(userId, out var cachedName))
            {
                userFullName = cachedName;
            }
            else
            {
                var userEntity = await _userRepository.GetByIdAsync(userId);
                userFullName = userEntity?.FullName ?? string.Empty;
                userNameCache[userId] = userFullName;
            }

            var userDto = new ProgressReviewByUserDto
            {
                UpdatedByFullName = userFullName,
                Periods = new Dictionary<string, ProgressReviewPeriodDto>()
            };

            if (userReports != null && userReports.Count > 0)
            {
                // Nhóm theo period (ngày/tuần/tháng)
                var periodGroups = userReports
                    .GroupBy(p => GetPeriodDate(p.UpdatedAt, frequencyType))
                    .OrderBy(g => g.Key)
                    .ToList();

                foreach (var periodGroup in periodGroups)
                {
                    var periodDate = periodGroup.Key;
                    var periodKey = GetPeriodKey(periodDate, frequencyType);

                    // Lấy báo cáo mới nhất trong kỳ
                    var latestReport = periodGroup.OrderByDescending(p => p.UpdatedAt).First();

                    var periodDto = new ProgressReviewPeriodDto
                    {
                        Status = latestReport.Status ?? string.Empty,
                        FilePath = latestReport.FilePath ?? string.Empty,
                        Proposal = latestReport.Proposal ?? string.Empty,
                        Result = latestReport.Result ?? string.Empty,
                        Feedback = latestReport.Feedback ?? string.Empty
                    };

                    userDto.Periods[periodKey] = periodDto;
                }
            }

            result.Add(userDto);
        }

        return result;
    }

    public async Task<List<SubTaskProgressReviewDto>> ReviewSubTaskProgressAsync(int taskId, DateTime? from, DateTime? to, string? status, int? assigneeId)
    {
        var records = await _progressRepository.GetProgressesForReviewAsync(taskId, from, to, status, assigneeId);
        var taskModel = await _taskRepository.GetTaskByIdAsync(taskId);
        if (taskModel == null)
            return new List<SubTaskProgressReviewDto>();

        var frequencyType = taskModel.Frequency?.FrequencyType?.ToLower() ?? "daily";
        var intervalValue = taskModel.Frequency?.IntervalValue ?? 1;

        var weeklyDaysRaw = taskModel.Frequency?.FrequencyDetails
            ?.Where(d => d.DayOfWeek.HasValue)
            .Select(d => d.DayOfWeek!.Value)
            .ToList() ?? new List<int>();

        var startDate = (taskModel.StartDate ?? DateTime.UtcNow).Date;
        var dueDate = (taskModel.DueDate ?? startDate).Date;
        if (startDate > dueDate)
            return new List<SubTaskProgressReviewDto>();

        var effectiveStart = from.HasValue ? (from.Value.Date > startDate ? from.Value.Date : startDate) : startDate;
        var effectiveEnd = to.HasValue ? (to.Value.Date < dueDate ? to.Value.Date : dueDate) : dueDate;
        if (effectiveStart > effectiveEnd)
            return new List<SubTaskProgressReviewDto>();

        var scheduledPeriods = GenerateScheduledPeriods(effectiveStart, effectiveEnd, frequencyType, intervalValue, weeklyDaysRaw);

        var userGroups = records
            .Where(p => p.UpdatedBy.HasValue)
            .GroupBy(p => p.UpdatedBy!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var authorizedUserIds = await _taskPermissionService.GetAuthorizedUserIdsAsync(taskId);
        if (assigneeId.HasValue)
        {
            authorizedUserIds = authorizedUserIds.Where(id => id == assigneeId.Value).ToList();
        }

        var result = new List<SubTaskProgressReviewDto>();
        var userNameCache = new Dictionary<int, string>();

        foreach (var userId in authorizedUserIds)
        {
            userGroups.TryGetValue(userId, out var userReports);
            var user = userReports?.FirstOrDefault()?.UpdatedByNavigation;
            string userFullName;
            if (user != null && !string.IsNullOrWhiteSpace(user.FullName))
            {
                userFullName = user.FullName;
            }
            else if (userNameCache.TryGetValue(userId, out var cachedName))
            {
                userFullName = cachedName;
            }
            else
            {
                var userEntity = await _userRepository.GetByIdAsync(userId);
                userFullName = userEntity?.FullName ?? string.Empty;
                userNameCache[userId] = userFullName;
            }

            var userDto = new SubTaskProgressReviewDto
            {
                UserId = userId,
                UserName = userFullName,
                ScheduledProgresses = new List<ScheduledProgressDto>()
            };

            for (int i = 0; i < scheduledPeriods.Count; i++)
            {
                var period = scheduledPeriods[i];
                var periodIndex = i + 1;

                // Ưu tiên PeriodIndex
                var periodProgresses = (userReports ?? new List<Core.Models.Progress>())
                    .Where(p =>
                    {
                        if (p.PeriodIndex.HasValue && p.PeriodIndex.Value > 0)
                        {
                            return p.PeriodIndex.Value == periodIndex;
                        }
                        else
                        {
                            return IsInPeriod(p.UpdatedAt, period.StartDate, period.EndDate, frequencyType);
                        }
                    })
                    .OrderByDescending(p => p.UpdatedAt)
                    .ToList();

                string overallStatus = "no_report";
                DateTime? latestSubmissionDate = null;

                if (periodProgresses.Any())
                {
                    var latestProgress = periodProgresses.First();
                    latestSubmissionDate = latestProgress.UpdatedAt;
                    overallStatus = latestProgress.Status ?? "pending";
                }
                else
                {
                    var now = DateTime.UtcNow;
                    if (now.Date > period.EndDate)
                    {
                        overallStatus = "missing";
                    }
                    else if (now.Date >= period.StartDate)
                    {
                        overallStatus = "no_report";
                    }
                    else
                    {
                        overallStatus = "upcoming";
                    }
                }

                var scheduledProgress = new ScheduledProgressDto
                {
                    PeriodIndex = periodIndex,
                    PeriodStartDate = period.StartDate,
                    PeriodEndDate = period.EndDate,
                    Status = overallStatus,
                    Date = latestSubmissionDate ?? DateTime.MinValue,
                    Progresses = new List<ProgressDetailDto>()
                };

                if (periodProgresses.Any())
                {
                    foreach (var progress in periodProgresses)
                    {
                        scheduledProgress.Progresses.Add(new ProgressDetailDto
                        {
                            ProgressId = progress.ProgressId,
                            Status = progress.Status ?? "pending",
                            UpdatedBy = progress.UpdatedBy ?? 0,
                            UpdateByName = progress.UpdatedByNavigation?.FullName,
                            Proposal = progress.Proposal,
                            Result = progress.Result,
                            Feedback = progress.Feedback,
                            UpdatedAt = progress.UpdatedAt,
                            FileName = progress.FileName,
                            FilePath = progress.FilePath ?? ""
                        });
                    }
                }
                else
                {
                    scheduledProgress.Progresses.Add(new ProgressDetailDto
                    {
                        ProgressId = 0,
                        Status = overallStatus == "missing" ? "Thiếu báo cáo (quá hạn)" : "Chưa có báo cáo",
                        UpdatedBy = 0,
                        UpdateByName = null,
                        Proposal = null,
                        Result = null,
                        Feedback = null,
                        UpdatedAt = null,
                        FileName = null,
                        FilePath = ""
                    });
                }

                userDto.ScheduledProgresses.Add(scheduledProgress);
            }

            result.Add(userDto);
        }

        return result;
    }

    /// <summary>
    /// Tính toán status dựa trên thời gian nộp so với kỳ báo cáo
    /// </summary>
    //private string CalculateProgressStatus(DateTime submittedAt, DateTime periodStart, DateTime periodEnd)
    //{
    //    var submittedDate = submittedAt.Date;

    //    if (submittedDate < periodStart)
    //    {
    //        return "Early"; // Nộp sớm (trước khi kỳ bắt đầu)
    //    }
    //    else if (submittedDate > periodEnd)
    //    {
    //        return "Late"; // Nộp muộn (sau deadline của kỳ)
    //    }
    //    else
    //    {
    //        return "OnTime"; // Nộp đúng hạn (trong khoảng thời gian của kỳ)
    //    }
    //}


    private List<(DateTime StartDate, DateTime EndDate)> GenerateScheduledPeriods(DateTime startDate, DateTime dueDate, string frequencyType, int intervalValue, List<int>? weeklyDaysRaw = null)
    {
        var periods = new List<(DateTime StartDate, DateTime EndDate)>();
        var current = startDate.Date;
        var endBoundary = dueDate.Date;

        if (intervalValue <= 0) intervalValue = 1;

        if (frequencyType == "weekly")
        {
            Console.WriteLine($"[PROGRESS-SERVICE-DEBUG] Weekly periods: start={startDate:yyyy-MM-dd}, end={endBoundary:yyyy-MM-dd}, interval={intervalValue}");
            Console.WriteLine($"[PROGRESS-SERVICE-DEBUG] Weekly days from DB: {string.Join(",", weeklyDaysRaw ?? new List<int>())}");

            // Sử dụng day từ database (1=Chủ nhật, 2=Thứ 2, ..., 7=Thứ 7)
            var targetDays = NormalizeWeeklyDays(weeklyDaysRaw ?? new List<int>()).ToList();
            Console.WriteLine($"[PROGRESS-SERVICE-DEBUG] Normalized days: {string.Join(",", targetDays)}");

            if (targetDays.Count == 0)
            {
                Console.WriteLine($"[PROGRESS-SERVICE-DEBUG] No valid days specified, using Sunday as default");
                targetDays.Add(DayOfWeek.Sunday);
            }

            // Tìm ngày báo cáo đầu tiên từ startDate
            var firstReportDay = FindNextReportDay(startDate, targetDays);
            var firstStart = startDate;
            var firstEnd = firstReportDay <= endBoundary ? firstReportDay : endBoundary;
            periods.Add((firstStart, firstEnd));
            Console.WriteLine($"[PROGRESS-SERVICE-DEBUG] Period 1: {firstStart:yyyy-MM-dd} to {firstEnd:yyyy-MM-dd} (first report period)");

            // Các kỳ tiếp theo: mỗi kỳ cách nhau intervalValue tuần, kết thúc vào ngày báo cáo
            var spanDays = 7 * intervalValue;
            var lastEnd = firstReportDay;
            int periodCount = 1;
            while (true)
            {
                var nextEnd = lastEnd.AddDays(spanDays);
                var nextStart = lastEnd.AddDays(1); // kỳ tiếp theo bắt đầu từ ngày sau kỳ trước

                // Chỉ tạo kỳ mới nếu có đủ thời gian cho ít nhất 1 ngày báo cáo
                if (nextStart > endBoundary) break;

                // Tìm ngày báo cáo gần nhất trong kỳ này
                var reportDay = FindNextReportDay(nextStart, targetDays);
                if (reportDay > endBoundary)
                {
                    Console.WriteLine($"[PROGRESS-SERVICE-DEBUG] Skipping period {periodCount + 1}: no report day in final period");
                    break;
                }

                var endDate = reportDay;

                // Chỉ thêm kỳ nếu có ít nhất 1 ngày (tránh kỳ rỗng)
                if (nextStart <= endDate)
                {
                    periods.Add((nextStart, endDate));
                    periodCount++;
                    Console.WriteLine($"[PROGRESS-SERVICE-DEBUG] Period {periodCount}: {nextStart:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                }
                lastEnd = nextEnd;
            }
            Console.WriteLine($"[PROGRESS-SERVICE-DEBUG] Total periods generated: {periods.Count}");
        }
        else
        {
            while (current <= endBoundary)
            {
                var endDate = frequencyType switch
                {
                    "daily" => current.AddDays(intervalValue - 1),
                    "monthly" => current.AddMonths(intervalValue).AddDays(-1),
                    _ => current.AddDays(intervalValue - 1)
                };

                if (endDate > endBoundary) endDate = endBoundary;

                periods.Add((current, endDate));

                current = frequencyType switch
                {
                    "daily" => current.AddDays(intervalValue),
                    "monthly" => current.AddMonths(intervalValue),
                    _ => current.AddDays(intervalValue)
                };
            }
        }

        return periods;
    }

    // Chuẩn hóa danh sách ngày tuần theo quy ước người dùng: 1=Chủ nhật, 2=Thứ 2,... -> .NET: Sunday=0..Saturday=6
    private List<DayOfWeek> NormalizeWeeklyDays(List<int>? days)
    {
        var result = new List<DayOfWeek>();
        if (days == null) return result;
        foreach (var d in days)
        {
            // Quy ước: 1=Chủ nhật, 2=Thứ 2, ..., 7=Thứ 7
            // .NET: 0=Chủ nhật, 1=Thứ 2, ..., 6=Thứ 7
            // Mapping: 1->0, 2->1, 3->2, 4->3, 5->4, 6->5, 7->6
            if (d >= 1 && d <= 7)
            {
                var dotnetDay = (DayOfWeek)((d - 1) % 7);
                result.Add(dotnetDay);
            }
        }
        return result;
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

    private bool IsInPeriod(DateTime date, DateTime periodStart, DateTime periodEnd, string frequencyType)
    {
        return frequencyType switch
        {
            "daily" => date.Date >= periodStart.Date && date.Date <= periodEnd.Date,
            "weekly" => date >= periodStart && date <= periodEnd,
            "monthly" => date >= periodStart && date <= periodEnd,
            _ => date.Date >= periodStart.Date && date.Date <= periodEnd.Date
        };
    }

    private DateTime GetPeriodDate(DateTime date, string frequencyType)
    {
        return frequencyType switch
        {
            "daily" => date.Date,
            "weekly" => GetWeekStart(date),
            "monthly" => new DateTime(date.Year, date.Month, 1),
            _ => date.Date
        };
    }

    private string GetPeriodKey(DateTime periodDate, string frequencyType)
    {
        return frequencyType switch
        {
            "daily" => periodDate.ToString("yyyy-MM-dd"),
            "weekly" => $"Week {GetWeekOfYear(periodDate)} - {periodDate.Year}",
            "monthly" => periodDate.ToString("yyyy-MM"),
            _ => periodDate.ToString("yyyy-MM-dd")
        };
    }

    private DateTime GetWeekStart(DateTime date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var monday = date.AddDays(-dayOfWeek + 1);
        return monday.Date;
    }

    private int GetWeekOfYear(DateTime date)
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        return culture.Calendar.GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
    }
    
    public async Task<List<ProgressDto>> GetLatestProgressesAsync(int top = 10)
    {
        return await _progressRepository.GetLatestProgressesAsync(top);
    }



    // thuy 

    public async Task<bool> AcceptProgressAsync(int progressId, int approverId)
    {
        var progress = await _progressRepository.GetProgressByIdAsync(progressId);
        if (progress == null)
            return false;
        // Kiểm tra quyền
        if (!await _taskPermissionService.CanDeleteTaskAsync(approverId, progress.TaskId))
            throw new UnauthorizedAccessException("Không có quyền phê duyệt báo cáo này.");

        if(progress.Status == "completed" || progress.Status == "late")
        {
            throw new InvalidOperationException("Báo cáo đã được phê duyệt không thể phê duyệt lại.");
        }

        string finalStatus = "completed";
        if(!string.IsNullOrEmpty(progress.Comment) && progress.Comment.Contains("PeriodEnd:")) {
            
            var parts = progress.Comment.Split('|');
            DateTime? periodEnd = null;
            DateTime? periodStart = null;

            foreach (var part in parts)
            {
                if (part.StartsWith("PeriodEnd:"))
                {
                    var dateStr = part.Replace("PeriodEnd:", "");
                    DateTime.TryParse(dateStr, out var parsedDate);
                    periodEnd = parsedDate;
                }
                if (part.StartsWith("PeriodStart:"))
                {
                    var dateStr = part.Replace("PeriodStart:", "");
                    DateTime.TryParse(dateStr, out var parsedDate);
                    periodStart = parsedDate;
                }
            }

            if(periodEnd.HasValue && progress.UpdatedAt.Date > periodEnd.Value)
            {
                finalStatus = "late";
            }
            else
            {
                finalStatus = "completed";
            }
        }
        
        // Cập nhật status = completed
        var update = new UpdateProgressRequest
        {
            Proposal = progress.Proposal,
            Result = progress.Result,
            Feedback = progress.Feedback,
            Comment = progress.Comment,
            Status = finalStatus,
            ReportFileName = progress.FileName,
            SubmittedByUserId = progress.UpdatedBy ?? approverId,
            PeriodIndex = progress.PeriodIndex
        };

        var updated = await _progressRepository.UpdateProgressAsync(progressId, update, progress.UpdatedBy);
        if (updated == null)
            return false;
        // Cập nhật lại  tiến độ
        await _progressCalculationService.CalculateTaskProgressAsync(updated.TaskId);

        // Tạo thông báo
        if (progress.UpdatedBy.HasValue)
        {
            var task = await _taskRepository.GetTaskByIdAsync(progress.TaskId);
            string message = $"Công việc {task?.Title} đã được phê duyệt.";
            var reminder = await _reminderService.CreateReminderAsync(
                task.TaskId, approverId, progress.UpdatedBy.Value, message);

            await _reminderService.SendRealTimeNotificationAsync(
                progress.UpdatedBy.Value,
                "Báo cáo được phê duyệt",
                message,
                new
                {
                    taskId = task?.TaskId,
                    title = task?.Title,
                    description = task?.Description,
                    startDate = task?.StartDate,
                    dueDate = task?.DueDate,
                    percentageComplete = task?.Percentagecomplete,
                    status = finalStatus,
                    isRead = reminder.IsRead
                });
        }

        return true;
    }



    public async Task<bool> RejectProgressAsync(int progressId, int approverId, string reason)
    {
        var progress = await _progressRepository.GetProgressByIdAsync(progressId);
        if (progress == null)
            return false;

        if (!await _taskPermissionService.CanDeleteTaskAsync(approverId, progress.TaskId))
            throw new UnauthorizedAccessException("Không có quyền từ chối báo cáo này.");

        if(progress.Status == "completed" || progress.Status == "late")
        {
            throw new InvalidOperationException("Báo cáo đã được phê duyệt không thể từ chối.");
        }

        //  Tạo nội dung message gửi thông báo và lưu vào comment
        var task = await _taskRepository.GetTaskByIdAsync(progress.TaskId);
        var message = $"Báo cáo tiến độ cho công việc '{task?.Title}' đã bị từ chối.  Lý do: {reason}";

        //  Cập nhật tiến độ
        var update = new UpdateProgressRequest
        {
            Proposal = progress.Proposal,
            Result = progress.Result,
            Feedback = reason,           // cột feedback
            Comment = message,           // cột comment
            Status = "rejected",
            ReportFileName = progress.FileName,
            SubmittedByUserId = progress.UpdatedBy ?? approverId
        };

        var updated = await _progressRepository.UpdateProgressAsync(progressId, update, progress.UpdatedBy);
        if (updated == null)
            return false;

        await _progressCalculationService.CalculateTaskProgressAsync(updated.TaskId);

        // Gửi thông báo cho người nộp
        if (progress.UpdatedBy.HasValue)
        {
            var reminder = await _reminderService.CreateReminderAsync(
                task.TaskId,
                approverId,
                progress.UpdatedBy.Value,
                message
            );

            await _reminderService.SendRealTimeNotificationAsync(
                progress.UpdatedBy.Value,
                "Báo cáo bị từ chối",
                message,
                new
                {
                    taskId = task?.TaskId,
                    title = task?.Title,
                    description = task?.Description,
                    startDate = task?.StartDate,
                    dueDate = task?.DueDate,
                    percentageComplete = task?.Percentagecomplete,
                    status = "rejected",
                    isRead = reminder.IsRead
                });
        }

        return true;
    }

    public async Task<List<UnitProgressReviewDto>> ReviewUnitProgressAsync(int taskId, DateTime? from, DateTime? to, string? status, int? unitId)
    {
        // Lấy thông tin task cùng các đơn vị phân công
        var taskModel = await _subTaskRepository.GetTaskUnitAssgment(taskId);

        if (taskModel == null)
        {
            return new List<UnitProgressReviewDto>();
        }

        if (taskModel.Taskunitassignments == null)
        {
            throw new Exception($"Không tìm thấy công việc được giao cho đơn vị");
        }


        // Tính toán thông tin về kỳ báo cáo
        var frequencyType = taskModel.Frequency?.FrequencyType?.ToLower() ?? "daily";
        var intervalValue = taskModel.Frequency?.IntervalValue ?? 1;

        // Lấy các ngày trong tuần
        var weeklyDaysRaw = taskModel.Frequency?.FrequencyDetails
            ?.Where(d => d.DayOfWeek.HasValue)
            .Select(d => d.DayOfWeek!.Value)
            .ToList() ?? new List<int>();

        var startDate = (taskModel.StartDate ?? DateTime.UtcNow).Date;
        var dueDate = (taskModel.DueDate ?? startDate).Date;
        if (startDate > dueDate)
            return new List<UnitProgressReviewDto>();

        var effectiveStart = from.HasValue ? (from.Value.Date > startDate ? from.Value.Date : startDate) : startDate;
        var effectiveEnd = to.HasValue ? (to.Value.Date < dueDate ? to.Value.Date : dueDate) : dueDate;
        if (effectiveStart > effectiveEnd)
            return new List<UnitProgressReviewDto>();

        var scheduledPeriods = GenerateScheduledPeriods(effectiveStart, effectiveEnd, frequencyType, intervalValue, weeklyDaysRaw);

        // Lọc unit assignments theo unitId nếu có
        var unitAssignments = taskModel.Taskunitassignments.ToList();
        if (unitId.HasValue)
        {
            unitAssignments = unitAssignments.Where(u => u.UnitId == unitId.Value).ToList();
        }

        if (unitAssignments.Count == 0)
        {
            throw new Exception($"Không tìm thấy đơn vị phù hợp với UnitId={unitId}");
        }

        // Lấy danh sách leaders của các đơn vị
        var leaderIds = new List<int>();
        var unitLeaderMap = new Dictionary<int, Core.Models.User>(); // UnitId -> Leader

        foreach (var unitAssignment in unitAssignments)
        {
            var leader = await _unitRepository.GetLeaderOfUnit(unitAssignment.UnitId);
            if (leader != null)
            {
                leaderIds.Add(leader.UserId);
                unitLeaderMap[unitAssignment.UnitId] = leader;
                Console.WriteLine($"[DEBUG] Unit {unitAssignment.UnitId} ({unitAssignment.Unit?.UnitName ?? "NULL"}): Leader {leader.FullName} (UserId: {leader.UserId})");
            }
            else
            {
                Console.WriteLine($"[DEBUG] Unit {unitAssignment.UnitId} ({unitAssignment.Unit?.UnitName ?? "NULL"}): No leader found");
            }
        }

        if (leaderIds.Count == 0)
        {
            Console.WriteLine($"[DEBUG] No leaders found for any unit");
            return new List<UnitProgressReviewDto>();
        }

        Console.WriteLine($"[DEBUG] Found {leaderIds.Count} leaders: {string.Join(", ", leaderIds)}");

        // 6. Lấy tất cả báo cáo của task
        var records = await _progressRepository.GetProgressesForReviewAsync(taskId, from, to, status, null);
        Console.WriteLine($"[DEBUG] Total progress records: {records.Count}");

        // 7. Tạo kết quả cho từng đơn vị
        var result = new List<UnitProgressReviewDto>();
        var userNameCache = new Dictionary<int, string>();

        foreach (var unitAssignment in unitAssignments)
        {
            var unit = unitAssignment.Unit;
            if (unit == null)
            {
                Console.WriteLine($"[DEBUG] ⚠️ Unit is null for assignment UnitId={unitAssignment.UnitId}");
                continue;
            }

            // Lấy thông tin leader
            if (!unitLeaderMap.TryGetValue(unit.UnitId, out var leader))
            {
                Console.WriteLine($"[DEBUG] No leader found for unit {unit.UnitId} ({unit.UnitName})");
                continue;
            }

            // Lấy báo cáo của leader này
            var leaderReports = records
                .Where(p => p.UpdatedBy.HasValue && p.UpdatedBy.Value == leader.UserId)
                .ToList();

            Console.WriteLine($"[DEBUG] Unit {unit.UnitName}: Leader {leader.FullName} has {leaderReports.Count} reports");

            string leaderFullName;
            if (!string.IsNullOrWhiteSpace(leader.FullName))
            {
                leaderFullName = leader.FullName;
            }
            else if (userNameCache.TryGetValue(leader.UserId, out var cachedName))
            {
                leaderFullName = cachedName;
            }
            else
            {
                var userEntity = await _userRepository.GetByIdAsync(leader.UserId);
                leaderFullName = userEntity?.FullName ?? leader.Username;
                userNameCache[leader.UserId] = leaderFullName;
            }

            var unitDto = new UnitProgressReviewDto
            {
                UnitId = unit.UnitId,
                UnitName = unit.UnitName,
                LeaderFullName = leaderFullName,
                userId = leader.UserId,
                ScheduledProgresses = new List<ScheduledProgressDto>()
            };

            Console.WriteLine($"[DEBUG] Processing unit: {unit.UnitName}, Leader: {leaderFullName}, Reports: {leaderReports.Count}");

            // 8. Tạo báo cáo cho từng kỳ
            for (int i = 0; i < scheduledPeriods.Count; i++)
            {
                var period = scheduledPeriods[i];
                var periodIndex = i + 1;

                // ✅ ƯU TIÊN PeriodIndex
                var periodProgresses = leaderReports
                    .Where(p =>
                    {
                        if (p.PeriodIndex.HasValue && p.PeriodIndex.Value > 0)
                        {
                            return p.PeriodIndex.Value == periodIndex;
                        }
                        else
                        {
                            return IsInPeriod(p.UpdatedAt, period.StartDate, period.EndDate, frequencyType);
                        }
                    })
                    .OrderByDescending(p => p.UpdatedAt)
                    .ToList();

                Console.WriteLine($"[DEBUG] Period {periodIndex} ({period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd}): {periodProgresses.Count} reports");

                string overallStatus = "no_report";
                DateTime? latestSubmissionDate = null;

                if (periodProgresses.Any())
                {
                    var latestProgress = periodProgresses.First();
                    latestSubmissionDate = latestProgress.UpdatedAt;
                    overallStatus = latestProgress.Status ?? "pending";
                }
                else
                {
                    var now = DateTime.UtcNow;
                    if (now.Date > period.EndDate)
                    {
                        overallStatus = "missing";
                    }
                    else if (now.Date >= period.StartDate)
                    {
                        overallStatus = "no_report";
                    }
                    else
                    {
                        overallStatus = "upcoming";
                    }
                }

                var scheduledProgress = new ScheduledProgressDto
                {
                    PeriodIndex = periodIndex,
                    PeriodStartDate = period.StartDate,
                    PeriodEndDate = period.EndDate,
                    Status = overallStatus,
                    Date = latestSubmissionDate ?? DateTime.MinValue,
                    Progresses = new List<ProgressDetailDto>()
                };

                if (periodProgresses.Any())
                {
                    foreach (var progress in periodProgresses)
                    {
                        scheduledProgress.Progresses.Add(new ProgressDetailDto
                        {
                            ProgressId = progress.ProgressId,
                            Status = progress.Status ?? "pending",
                            UpdatedBy = progress.UpdatedBy ?? 0,
                            UpdateByName = progress.UpdatedByNavigation?.FullName,
                            Proposal = progress.Proposal,
                            Result = progress.Result,
                            Feedback = progress.Feedback,
                            UpdatedAt = progress.UpdatedAt,
                            FileName = progress.FileName,
                            FilePath = progress.FilePath ?? ""
                        });
                    }
                }
                else
                {
                    scheduledProgress.Progresses.Add(new ProgressDetailDto
                    {
                        ProgressId = 0,
                        Status = overallStatus == "missing" ? "Thiếu báo cáo (quá hạn)" : "Chưa có báo cáo",
                        UpdatedBy = 0,
                        UpdateByName = null,
                        Proposal = null,
                        Result = null,
                        Feedback = null,
                        UpdatedAt = null,
                        FileName = null,
                        FilePath = ""
                    });
                }

                unitDto.ScheduledProgresses.Add(scheduledProgress);
            }

            result.Add(unitDto);
        }

        return result;
    }


    public async Task<PaginatedList<ReviewUserProgressResponse>> ReviewUserProgressAsync(int userId, string? search, PageOptionsRequest pageOptions, DateTime? from, DateTime? to, string? status)
    {

        try
        {
            // 1. Lấy danh sách taskIds được phân công
            var assignedTaskIds = await _taskRepository.GetAssignedTaskIdsForUserAsync(userId, search);

            if (assignedTaskIds == null || assignedTaskIds.Count == 0)
            {
                return new PaginatedList<ReviewUserProgressResponse>();
            }

            var results = new List<ReviewUserProgressResponse>();

            // 2. Duyệt từng task
            foreach (var taskId in assignedTaskIds)
            {

                var task = await _taskRepository.GetTaskByIdAsync(taskId);
                if (task == null)
                {
                    continue;
                }


                // 3. Kiểm tra frequency
                if (task.Frequency == null)
                {
                    continue;
                }


                var frequencyType = task.Frequency.FrequencyType?.ToLower() ?? "daily";
                var intervalValue = task.Frequency.IntervalValue;

                var weeklyDaysRaw = task.Frequency.FrequencyDetails
                    ?.Where(d => d.DayOfWeek.HasValue)
                    .Select(d => d.DayOfWeek!.Value)
                    .ToList() ?? new List<int>();

                var startDate = (task.StartDate ?? DateTime.UtcNow).Date;
                var dueDate = (task.DueDate ?? startDate.AddDays(30)).Date; // Default 30 days if no due date

                if (startDate > dueDate)
                {
                    continue;
                }

                var effectiveStart = from.HasValue ? (from.Value.Date > startDate ? from.Value.Date : startDate) : startDate;
                var effectiveEnd = to.HasValue ? (to.Value.Date < dueDate ? to.Value.Date : dueDate) : dueDate;

                if (effectiveStart > effectiveEnd)
                {
                    continue;
                }

                // 4. Tạo kỳ báo cáo
                var scheduledPeriods = GenerateScheduledPeriods(effectiveStart, effectiveEnd, frequencyType, intervalValue, weeklyDaysRaw);

                if (scheduledPeriods.Count == 0)
                {
                    continue;
                }

                // 5. Lấy tất cả progress của user trong task
                var progressList = await _progressRepository.GetProgressesForReviewAsync(taskId, from, to, status, userId);

                var mapper = new ProgressMapper();
                var taskDto = mapper.FromTaskToReviewUserProgressResponse(task, frequencyType, intervalValue);

                int completedCount = 0, missingCount = 0, lateCount = 0, pendingCount = 0, upcomingCount = 0;

                // 6. Xử lý từng kỳ
                for (int i = 0; i < scheduledPeriods.Count; i++)
                {
                    var period = scheduledPeriods[i];
                    var periodIndex = i + 1;

                    var periodProgresses = progressList
                        .Where(p =>
                        {
                            if (p.PeriodIndex.HasValue && p.PeriodIndex.Value > 0)
                            {
                                return p.PeriodIndex.Value == periodIndex;
                            }
                            else
                            {
                                return IsInPeriod(p.UpdatedAt, period.StartDate, period.EndDate, frequencyType);
                            }
                        })
                        .OrderByDescending(p => p.UpdatedAt)
                        .ToList();

                    string finalStatus = "no_report";
                    DateTime? latestSubmissionDate = null;

                    if (periodProgresses.Any())
                    {
                        var latestProgress = periodProgresses.First();
                        latestSubmissionDate = latestProgress.UpdatedAt;
                        finalStatus = latestProgress.Status ?? "pending";

                        switch (finalStatus.ToLower())
                        {
                            case "completed":
                                completedCount++;
                                break;
                            case "late":
                                lateCount++;
                                break;
                            case "pending":
                                pendingCount++;
                                break;
                        }
                    }
                    else
                    {
                        var now = DateTime.UtcNow;
                        if (now.Date > period.EndDate)
                        {
                            finalStatus = "missing";
                            missingCount++;
                        }
                        else if (now.Date >= period.StartDate)
                        {
                            finalStatus = "no_report";
                        }
                        else
                        {
                            finalStatus = "upcoming";
                            upcomingCount++;
                        }
                    }

                    var scheduledProgress = new ScheduledProgressDto
                    {
                        PeriodIndex = periodIndex,
                        PeriodStartDate = period.StartDate,
                        PeriodEndDate = period.EndDate,
                        Status = finalStatus,
                        Date = latestSubmissionDate ?? DateTime.MinValue,
                        Progresses = new List<ProgressDetailDto>()
                    };

                    if (periodProgresses.Any())
                    {
                        foreach (var progressItem in periodProgresses)
                        {
                            scheduledProgress.Progresses.Add(new ProgressDetailDto
                            {
                                ProgressId = progressItem.ProgressId,
                                Status = progressItem.Status ?? "pending",
                                UpdatedBy = progressItem.UpdatedBy ?? 0,
                                UpdateByName = progressItem.UpdatedByNavigation?.FullName,
                                Proposal = progressItem.Proposal,
                                Result = progressItem.Result,
                                Feedback = progressItem.Feedback,
                                UpdatedAt = progressItem.UpdatedAt,
                                FileName = progressItem.FileName,
                                FilePath = progressItem.FilePath ?? ""
                            });
                        }
                    }
                    else
                    {
                        scheduledProgress.Progresses.Add(new ProgressDetailDto
                        {
                            ProgressId = 0,
                            Status = finalStatus == "missing" ? "Thiếu báo cáo (quá hạn)" :
                                     finalStatus == "upcoming" ? "Chưa đến kỳ báo cáo" : "Chưa có báo cáo",
                            UpdatedBy = 0,
                            UpdateByName = null,
                            Proposal = null,
                            Result = null,
                            Feedback = null,
                            UpdatedAt = null,
                            FileName = null,
                            FilePath = ""
                        });
                    }

                    taskDto.scheduledProgress.Add(scheduledProgress);
                }

                // 7. Tính summary
                int totalPeriods = scheduledPeriods.Count;
                taskDto.Summary = new ProgressSummary
                {
                    TotalPeriods = scheduledPeriods.Count,
                    CompletedPeriods = completedCount,
                    LateReports = lateCount,
                    PendingReports = pendingCount,
                    MissedReports = missingCount,
                    upcomingReports = upcomingCount,
                    CompletedRate = totalPeriods > 0
                        ? Math.Round((double)(completedCount + lateCount) / totalPeriods * 100, 2)
                        : 0
                };
                results.Add(taskDto);
            }

            results = results
            .OrderByDescending(r => r.StartDate ?? r.DueDate ?? DateTime.MinValue)
            .ToList();

            var totalCount = results.Count;
            var items = results
                .Skip((pageOptions.Page - 1) * pageOptions.Size)
                .Take(pageOptions.Size)
                .ToList();

            return new PaginatedList<ReviewUserProgressResponse>(items, totalCount, pageOptions.Page, pageOptions.Size);
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}


