using DocTask.Core.Dtos.Authentications;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using DocTask.Data.Repositories;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using TaskModel = DocTask.Core.Models.Task;

namespace DocTask.Service.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _userRepository;
    private readonly IJwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthenticationService(IUserRepository userRepository, IJwtService jwtService, IEmailService emailService, IConfiguration config)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _emailService = emailService;
        _config = config;
    }

    public async System.Threading.Tasks.Task<LoginResponseDto> Login(LoginRequestDto request)
    {
        var foundUser = await _userRepository.GetByUserNameAsync(request.Username);
        if (foundUser == null || !BCrypt.Net.BCrypt.Verify(request.Password, foundUser.Password))
            throw new BadRequestException("Username or password is incorrect");

        //foundUser!.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var refresh = _jwtService.GenerateRefreshToken(foundUser!);
        var updatedUser = await _userRepository.UpdateRefreshToken(foundUser, refresh);
        
        return new LoginResponseDto
        {
            AccessToken = _jwtService.GenerateAccessToken(foundUser!),
            RefreshToken =  refresh
        };
    }

    public async System.Threading.Tasks.Task Logout(string accessToken, string refreshToken)
    {
        var jwtToken = (JwtSecurityToken)_jwtService.ValidateAccessToken(accessToken);
        _jwtService.ValidateRefreshToken(refreshToken);
        var username = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
        if (string.IsNullOrEmpty(username))
            throw new UnauthorizedException("Invalid token");
        var user = await _userRepository.GetByUserNameAsync(username);
        if (user?.Refreshtoken == null || !user.Refreshtoken.Equals(refreshToken))
            throw new UnauthorizedException("Invalid token");
        await _userRepository.UpdateRefreshToken(user, null);
    }

    public async System.Threading.Tasks.Task<RefreshResponseDto> RefreshToken(string refreshToken)
    {
        var jwtToken = (JwtSecurityToken)_jwtService.ValidateRefreshToken(refreshToken);
        var username = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid")?.Value;
        if (string.IsNullOrEmpty(username))
            throw new UnauthorizedException("Invalid token");
        var user = await _userRepository.GetByUserNameAsync(username);
        if (user?.Refreshtoken == null || !user.Refreshtoken.Equals(refreshToken))
            throw new UnauthorizedException("Invalid token");
        
        var newAccessToken = _jwtService.GenerateAccessToken(user);
        var newRefreshToken = _jwtService.GenerateRefreshToken(user);
        
        var updatedUser = await _userRepository.UpdateRefreshToken(user, newRefreshToken);
        
        return new RefreshResponseDto(newAccessToken, newRefreshToken);
    }

    public async System.Threading.Tasks.Task ChangePassword(int userId, ChangePasswordRequestDto request)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
            throw new NotFoundException("User not found");

        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password))
            throw new BadRequestException("Old password is incorrect");

        var newHashed = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _userRepository.UpdatePasswordAsync(user, newHashed);
    }

    public async System.Threading.Tasks.Task ForgotPasswordAsync(string username)
    {
        var user = await _userRepository.GetByUserNameAsync(username) ?? throw new NotFoundException("Khong tim thay tai khoan");

        if (string.IsNullOrEmpty(user.Email))
            throw new Exception("Tài khoản này chưa có email khôi phục.");

        user.ResetToken = Guid.NewGuid().ToString("N");
        user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(10);

        await _userRepository.SaveChanges();

        var frontendUrl = _config["FrontendSettings:BaseUrl"] ?? "http://localhost:4200";
        var link = $"{frontendUrl}/reset-password?token={user.ResetToken}";

        var body = $"<p>Yêu cầu đặt lại mật khẩu của bạn:</p><a href='{link}'>{link}</a>";

        await _emailService.SendEmailAsync(user.Email, "Yêu cầu đặt lại mật khẩu", body);

    }

    public async System.Threading.Tasks.Task ResetPasswordAsync(string token, string newPassword)
    {
        var user = await _userRepository.GetTokenAsync(token) ?? throw new NotFoundException("Token không hợp lệ.");

        if (user.ResetTokenExpiry < DateTime.UtcNow)
            throw new Exception("Token đã hết hạn.");

        if (BCrypt.Net.BCrypt.Verify(newPassword, user.Password))
            throw new BadRequestException("Mật khẩu mới không được giống mật khẩu cũ.");

        user.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;
        await _userRepository.SaveChanges();
    }
}