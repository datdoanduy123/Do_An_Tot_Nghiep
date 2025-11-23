using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Repositories;

public interface IUserRepository

{

    System.Threading.Tasks.Task<User?> GetByIdAsync(int userId);
    System.Threading.Tasks.Task<User?> GetByUserNameAsync(string username);
    System.Threading.Tasks.Task<User> UpdateRefreshToken(User user, string? refreshToken);
    System.Threading.Tasks.Task UpdatePasswordAsync(User user, string newHashedPassword);
    System.Threading.Tasks.Task<DocTask.Core.Dtos.Users.CurrentUserDto?> GetCurrentUserAsync(int userId);
    System.Threading.Tasks.Task<DocTask.Core.Dtos.Users.CurrentUserDto?> UpdateCurrentUserAsync(int userId, DocTask.Core.Dtos.Users.UpdateCurrentUserRequestDto dto);
    System.Threading.Tasks.Task<(System.Collections.Generic.List<DocTask.Core.Dtos.Users.UserBasicDto> subordinates, System.Collections.Generic.List<DocTask.Core.Dtos.Users.UserBasicDto> peers)> GetSubordinatesAndPeersAsync(int callerId);
    Task<User?> GetByIdWithUnitUserAsync(int userId);
    Task<List<User>> GetAllByParentUserId(int userParentId);
    Task<User?> GetTokenAsync(string token);
    Task<int> SaveChanges();
}