namespace DocTask.Core.Dtos.Tasks;

public class SubtaskCreateRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public List<int>? AssigneeIds { get; set; }
    public List<int>? UnitIds { get; set; }

    public string? FrequencyType { get; set; } // e.g., "daily", "weekly", "monthly"
    public int? IntervalValue { get; set; }
    public List<int>? DaysOfWeek { get; set; }
    public List<int>? DaysOfMonth { get; set; }

    public int? ParentTaskId { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}



