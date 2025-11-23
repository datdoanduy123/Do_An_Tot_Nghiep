namespace DocTask.Core.Dtos.Users;

public class UserBasicDto
{
    public int UserId { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public int? OrgId { get; set; }
    public int? UnitId { get; set; }
    public string? Role { get; set; }
}


