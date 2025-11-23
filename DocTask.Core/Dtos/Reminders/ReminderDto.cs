namespace DocTask.Core.Dtos.Reminders;

public class ReminderDto
{
    public int Reminderid { get; set; }
    public int Taskid { get; set; }
    public int? Periodid { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Triggertime { get; set; }
    public bool? Isauto { get; set; }
    public int? Createdby { get; set; }
    public DateTime? Createdat { get; set; }
    public bool? Isnotified { get; set; }
    public DateTime? Notifiedat { get; set; }
    public int? Notificationid { get; set; }
    public int? UserId { get; set; }
}

