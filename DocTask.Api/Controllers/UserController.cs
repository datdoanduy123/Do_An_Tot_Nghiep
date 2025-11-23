using System.Security.Claims;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Dtos.Authentications;
using DocTask.Core.Dtos.Users;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DocTask.Api.Controllers;

[ApiController]
[Route("/api/v1/user")]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IAuthenticationService _authenticationService;

    public UserController(IUserRepository userRepository, IAuthenticationService authenticationService)
    {
        _userRepository = userRepository;
        _authenticationService = authenticationService;
    }

    [HttpGet("profile")]
    [SwaggerOperation(Summary = "Lấy thông tin người dùng hiện tại.")]
    public async Task<IActionResult> GetCurrentUserAsync()
    {
        var userId = GetUserIdFromHttpContext();
        if (userId == null)
        {
            throw new UnauthorizedException("Không thể xác thực người dùng.");
        }

        var user = await _userRepository.GetCurrentUserAsync(userId.Value);

        return Ok(new ApiResponse<CurrentUserDto?>
        {
            Data = user,
            Message = "Lấy thông tin người dùng thành công."
        });
    }


    [HttpPatch("change-profile")]
    [SwaggerOperation(Summary = "Sửa thông tin của người dùng hiện tại")]
    public async Task<IActionResult> ChangeProfileAsync([FromBody] DocTask.Core.Dtos.Users.UpdateCurrentUserRequestDto request)
    {
        var userId = GetUserIdFromHttpContext();
        if (userId == null)
        {
            throw new UnauthorizedException("Không thể xác thực người dùng.");
        }

        var updated = await _userRepository.UpdateCurrentUserAsync(userId.Value, request);
        if (updated == null)
        {
            throw new DocTask.Core.Exceptions.NotFoundException("Người dùng không tồn tại.");
        }

        return Ok(new ApiResponse<DocTask.Core.Dtos.Users.CurrentUserDto?>
        {
            Data = updated,
            Message = "Cập nhật thông tin người dùng thành công."
        });
    }

    [HttpPost("change-password")]
    [SwaggerOperation(Summary = "Đổi mật khẩu cho người dùng hiện tại.")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        var userId = GetUserIdFromHttpContext();
        if (userId == null)
        {
            throw new UnauthorizedException("Không thể xác thực người dùng.");
        }

        await _authenticationService.ChangePassword(userId.Value, request);

        return Ok(new ApiResponse<object>
        {
            Message = "Đổi mật khẩu thành công. Vui lòng đăng nhập lại."
        });
    }
    private int? GetUserIdFromHttpContext()
    {
        var idClaim = HttpContext.User.FindFirst("id");
        if (idClaim == null) return null;
        if (int.TryParse(idClaim.Value, out var id)) return id;
        return null;
    }
}


