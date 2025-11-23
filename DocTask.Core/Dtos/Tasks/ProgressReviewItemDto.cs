namespace DocTask.Core.Dtos.Tasks;

public class ProgressReviewItemDto
{
    public int ProgressId { get; set; }
    public int TaskId { get; set; }
    public int? UpdatedBy { get; set; }
    public string? UpdatedByUserName { get; set; }
    public string? UpdatedByFullName { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? Status { get; set; }
    public string? Result { get; set; }
    public string? Proposal { get; set; }
    public string? Feedback { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public bool IsOnTime { get; set; }
}

public class ProgressReviewByUserDto
{
    public string UpdatedByFullName { get; set; } = string.Empty;
    public Dictionary<string, ProgressReviewPeriodDto> Periods { get; set; } = new Dictionary<string, ProgressReviewPeriodDto>();
}

public class ProgressReviewPeriodDto
{
    public string Status { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Proposal { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public string Feedback { get; set; } = string.Empty;
}

public class SubTaskProgressReviewDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public List<ScheduledProgressDto> ScheduledProgresses { get; set; } = new List<ScheduledProgressDto>();
}

public class ScheduledProgressDto
{
    public int PeriodIndex { get; set; }
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public List<ProgressDetailDto> Progresses { get; set; } = new List<ProgressDetailDto>();
    public string Status { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

public class ProgressDetailDto
{
    public int ProgressId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int UpdatedBy { get; set; }
    public string? UpdateByName { get; set; }
    public string? Proposal { get; set; }
    public string? Result { get; set; }
    public string? Feedback { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? FileName { get; set; }
    public string FilePath { get; set; } = string.Empty;
}



