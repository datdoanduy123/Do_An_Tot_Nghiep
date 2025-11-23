namespace DocTask.Core.Dtos.Users;

public class CurrentUserDto
{
    public int UserId { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Role { get; set; }
    public string? PositionName { get; set; }
    public string? OrgName{ get; set; }
    public string? UnitName{ get; set; }
}


