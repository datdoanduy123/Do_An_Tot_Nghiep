namespace DocTask.Core.Dtos.Tasks;

public class UpdateProgressRequest
{
    public string? Proposal { get; set; }
    public string? Result { get; set; }
    public string? Feedback { get; set; }
    public string? Comment { get; set; } = string.Empty;
    public string Status { get; set; } = "in_progress";

    // File inputs (optional)
    public string? ReportFileName { get; set; }
    public Stream? ReportFileStream { get; set; }
    public string? ReportFilePath { get; set; }
    public int? PeriodIndex { get; set; }


    // Submitter
    public int SubmittedByUserId { get; set; }
}
