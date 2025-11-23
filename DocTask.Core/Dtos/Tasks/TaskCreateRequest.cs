namespace DocTask.Core.Dtos.Tasks;

public class TaskCreateRequest
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? Priority { get; set; }

    // Accept ISO date-times like "2025-09-13T08:35:59.000Z" and map to DateOnly in service/mapping layer
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
}



