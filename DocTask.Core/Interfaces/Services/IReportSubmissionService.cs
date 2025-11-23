using DocTask.Core.DTOs.Reports;

namespace DocTask.Core.Interfaces.Services;

/// <summary>
/// Interface cho service xử lý logic nộp báo cáo
/// </summary>
public interface IReportSubmissionService
{
    /// <summary>
    /// Kiểm tra xem user có thể nộp báo cáo cho task không
    /// </summary>
    /// <param name="userId">ID của user</param>
    /// <param name="taskId">ID của task</param>
    /// <returns>Kết quả kiểm tra</returns>
    Task<ReportSubmissionResult> CanSubmitReportAsync(int userId, int taskId);
}
