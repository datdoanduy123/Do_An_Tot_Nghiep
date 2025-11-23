namespace DocTask.Core.DTOs.Reminders;

public class CreateReminderRequestDto
{
    public int TaskId { get; set; }
    public int UserId { get; set; }
    public string Message { get; set; } = string.Empty;
}
