using DocTask.Core.Dtos.Users;
using DocTask.Core.Models;

namespace DocTask.Service.Mappers;

public static class UserMapper
{
    public static UserDto ToUserDto(this User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = user.FullName,
            Username = user.Username,
        };
    }
}