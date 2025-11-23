using DocTask.Core.Dtos.Authentications;

namespace DocTask.Core.Interfaces.Services;

public interface IAuthenticationService
{
    System.Threading.Tasks.Task<LoginResponseDto> Login(LoginRequestDto request);
    System.Threading.Tasks.Task Logout(string accessToken, string refreshToken);
    System.Threading.Tasks.Task<RefreshResponseDto> RefreshToken(string refreshToken);
    System.Threading.Tasks.Task ChangePassword(int userId, ChangePasswordRequestDto request);
    System.Threading.Tasks.Task ForgotPasswordAsync (string username);
    System.Threading.Tasks.Task ResetPasswordAsync(string token, string newPassword);
}