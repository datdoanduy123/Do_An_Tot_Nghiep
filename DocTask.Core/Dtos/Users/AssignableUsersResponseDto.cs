namespace DocTask.Core.Dtos.Users;

public class AssignableUsersResponseDto
{
    public List<UserDto> subordinates { get; set; } = [];
    public List<UserDto> peers { get; set;}= [];
}