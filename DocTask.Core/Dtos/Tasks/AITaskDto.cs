public class AITaskDTO
{
  public string Title { get; set; } = "";
  public string Description { get; set; } = "";
  public string StartDate { get; set; } = "";
  public string EndDate { get; set; } = "";
  public List<AiSubTaskDto> Subtasks { get; set; } = new();
}

public class AiSubTaskDto
{
  public string Title { get; set; } = "";
  public string Description { get; set; } = "";
  public string StartDate { get; set; } = "";
  public string DueDate { get; set; } = "";
  public string Frequency { get; set; } = "";
  public string AssignedUserIds { get; set; } = "";
  public string AssignedUnitIds { get; set; } = "";
}