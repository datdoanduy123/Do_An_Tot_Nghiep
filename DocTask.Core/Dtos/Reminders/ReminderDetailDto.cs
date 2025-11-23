using DocTask.Core.Dtos.Tasks;

namespace DocTask.Core.Dtos.Reminders;

public class ReminderDetailDto
{
    public int ReminderId { get; set; }
    public TaskDto Task { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? CreatedBy { get; set; }
    public DateTime? CreatedAt { get; set; }
    public bool? IsRead { get; set; }
    public int? UserId { get; set; }
    public bool? IsOwner { get; set; }
}