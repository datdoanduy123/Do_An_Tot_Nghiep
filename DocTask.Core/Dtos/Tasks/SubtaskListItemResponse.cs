namespace DocTask.Core.Dtos.Tasks;

public class SubtaskListItemResponse
{
    public int TaskId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public int PercentageComplete { get; set; }
    public List<string> AssigneeFullNames { get; set; } = new();
    public string? FrequencyType { get; set; }
    public int? IntervalValue { get; set; }
}



