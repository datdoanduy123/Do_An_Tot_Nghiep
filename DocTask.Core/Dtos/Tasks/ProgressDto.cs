namespace DocTask.Core.Dtos.Tasks;

public class ProgressDto
{
    public int ProgressId { get; set; }
    public int TaskId { get; set; }
    public int? PeriodId { get; set; }
    public int? PercentageComplete { get; set; }
    public string? Comment { get; set; }
    public string? Status { get; set; }
    public int? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public string? Proposal { get; set; }
    public string? Result { get; set; }
    public string? Feedback { get; set; }
    
    // Thông tin người cập nhật (chỉ cần thông tin cơ bản)
    public string? UpdatedByUserName { get; set; }
    public string? UpdatedByFullName { get; set; }
}
