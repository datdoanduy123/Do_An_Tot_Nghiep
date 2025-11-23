using DocTask.Core.Dtos.Authentications;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DocTask.Api.Controllers;

[ApiController]
[Route("/api/v1/auth")]
[SwaggerTag("Xác thực")]

public class AuthenticationController : ControllerBase
{
    private IAuthenticationService _authenticationService;

    public AuthenticationController(IAuthenticationService authenticationService)
    {
        _authenticationService = authenticationService;
    }

    [HttpPost("login")]
    [SwaggerOperation(Summary = "Đăng nhập", Description = "Trả về access token và refresh token")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var result = await _authenticationService.Login(request);
        return Ok(new ApiResponse<LoginResponseDto>
        {
            Data = result,
            Message = "Login success"
        });
    }

    [HttpPost("logout")]
    [SwaggerOperation(Summary = "Đăng xuất", Description = "Trả ra message khi đăng xuất thành công")]
    public async Task<IActionResult> Logout([FromHeader] string accessToken, [FromHeader] string refreshToken)
    {
        await _authenticationService.Logout(accessToken, refreshToken);

        return Ok(new ApiResponse<object>
        {
            Message = "Logout success"
        });
    }

    [HttpPost("refresh")]
    [SwaggerOperation(Summary = "Làm mới token", Description = "Trả ra về access token và refresh token mới")]
    public async Task<IActionResult> Refresh([FromHeader] string refreshToken)
    {
        var result = await _authenticationService.RefreshToken(refreshToken);
        return Ok(new ApiResponse<RefreshResponseDto>
        {
            Data = result,
            Message = "Refresh token success"
        });
    }


    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Quên mật khẩu", Description = "Nhập tên tài khoản để gửi email")]

    public async Task<IActionResult> ForgotPassword([FromBody] string username)
    {
        await _authenticationService.ForgotPasswordAsync(username);
        return Ok(new ApiResponse<object>
        {
            Message = "Đã gửi email đặt lại mật khẩu."
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [SwaggerOperation(Summary = "Thay đổi mật khẩu", Description = "Lấy token trong mail và nhập mật khẩu mới")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
    {
         await _authenticationService.ResetPasswordAsync(request.Token, request.NewPassword);
        return Ok(new ApiResponse<object>
        {
            Message = "Đặt lại mật khẩu thành công."
        });
    }
}