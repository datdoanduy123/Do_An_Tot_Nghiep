namespace DocTask.Core.Dtos.Users;

public class UpdateCurrentUserRequestDto
{
  public string? FullName { get; set; }
  public string? Email { get; set; }
  public string? PhoneNumber { get; set; }
}
