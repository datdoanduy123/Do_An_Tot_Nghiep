namespace DocTask.Core.Dtos.Tasks;

public class ProgressCalculationResponse
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public int TotalProgressRecords { get; set; }
    public int TotalRequiredReports { get; set; }
    public double ProgressPercentage { get; set; }
    public bool IsParentTask { get; set; }
    public List<ChildTaskProgress>? ChildTasks { get; set; }
}

public class ChildTaskProgress
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public int TotalProgressRecords { get; set; }
    public int TotalRequiredReports { get; set; }
    public double ProgressPercentage { get; set; }
}
