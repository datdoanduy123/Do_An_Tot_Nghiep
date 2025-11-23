namespace DocTask.Core.DTOs.Reports;

/// <summary>
/// Kết quả kiểm tra nộp báo cáo
/// </summary>
public class ReportSubmissionResult
{
    public bool CanSubmit { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public int TotalSubmitted { get; set; }
    public int TotalRequired { get; set; }
}
