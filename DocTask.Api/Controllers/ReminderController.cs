using DocTask.Core.Dtos.Reminders;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.DTOs.Reminders;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Paginations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using System.Security.Claims;
using System.Threading;

namespace DocTask.Api.Controllers;

[ApiController]
[Route("/api/v1/reminder")]
public class ReminderController : ControllerBase
{
    private readonly IReminderService _reminderService;

    public ReminderController(IReminderService reminderService)
    {
        _reminderService = reminderService;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetAllReminders([FromQuery] PageOptionsRequest pageOptions)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
        int userId = int.Parse(userIdClaim);

        var reminders = await _reminderService.GetRemindersByUserId(userId, pageOptions);

        return Ok(new ApiResponse<object>
        {
            Data = reminders,
            Message = "Lấy danh sách nhắc nhở thành công."
        });
    }

    //[HttpPost]
    //[SwaggerOperation(Summary = "Tạo mới nhắc nhở", Description = "Trả về nhắc nhở đã được tạo")]
    //public async Task<IActionResult> CreateReminderAsync([FromBody] CreateReminderRequestDto request)
    //{
    //    //var reminder = await _reminderService.CreateReminderAsync(request.TaskId, request.UserId, request.Message);
    //    var createdBy = int.Parse(User.FindFirst("id")?.Value ?? "0");

    //    var reminder = await _reminderService.CreateReminderAsync(request.TaskId, createdBy, request.UserId, request.Message);
    //    return Ok(new ApiResponse<object>
    //    {
    //        Data = new
    //        {
    //            reminder.Reminderid,
    //            reminder.Title,
    //            reminder.Message,
    //            reminder.Triggertime,
    //            reminder.Createdat,
    //            TaskId = reminder.Taskid,
    //            reminder.UserId,
    //            IsRead = false
    //        },
    //        Message = "Tạo nhắc nhở thành công."
    //    });
    //}

    [HttpPost]
    [SwaggerOperation(Summary = "Tạo mới nhắc nhở", Description = "Trả về nhắc nhở đã được tạo")]
    public async Task<IActionResult> CreateReminderAsync([FromBody] CreateReminderRequestDto request)
    {
        //var reminder = await _reminderService.CreateReminderAsync(request.TaskId, request.UserId, request.Message);
        var createdBy = int.Parse(User.FindFirst("id")?.Value ?? "0");

        var reminder = await _reminderService.CreateReminderWithNotificationAsync(request.TaskId, createdBy, request.UserId, request.Message);
        return Ok(new ApiResponse<object>
        {
            Data = new
            {
                reminder.Reminderid,
                reminder.Title,
                reminder.Message,
                reminder.Triggertime,
                reminder.Createdat,
                TaskId = reminder.Taskid,
                reminder.UserId,
                IsRead = false
            },
            Message = "Tạo nhắc nhở thành công."
        });
    }

    [HttpGet("unread/count")]
    [SwaggerOperation(Summary = "Lấy số lượng nhắc nhở chưa đọc", Description = "")]
    public async Task<IActionResult> GetUnreadReminderCount()
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
        int userId = int.Parse(userIdClaim);


        var result = await _reminderService.GetUnreadReminderCount(userId);

        return Ok(new ApiResponse<int>
        {
            Data = result,
            Message = "Lấy số lượng nhắc nhở chưa được thành công!"
        });
    }

    [HttpDelete("delete/{reminderId}")]
    [Authorize(Roles = "Admin, User")]
    public async Task<IActionResult> DeleteReminderAsync(int reminderId)
    {
        var result = await _reminderService.DeleteReminderAsync(reminderId);

        return Ok(new ApiResponse<bool>
        {
            Data = result,
            Message = "Lấy số lượng nhắc nhở chưa được thành công!"
        });
    }
    
    [HttpPatch("read/{reminderId:int}")]
    [SwaggerOperation(Summary = "Đọc nhắc nhở", Description = "Trạng thái IsRead của nhắc nhở dược set về true")]
    public async Task<IActionResult> ReadReminder([FromRoute] int reminderId)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
        int userId = int.Parse(userIdClaim);

        
        var result = await _reminderService.ReadReminder(userId, reminderId);

        return Ok(new ApiResponse<bool>
        {
            Data = result,
            Message = "Thành công!"
        });
    }



    [HttpPost("unit/{taskId:int}/{unitId:int}")]
    [SwaggerOperation(
    Summary = "Tạo nhắc nhở cho phòng ban", 
    Description = "Tạo nhắc nhở cho phòng ban, người đứng đầu phòng ban sẽ nhận được thông báo")]
    public async Task<IActionResult> CreateReminderForUnit(
        [FromRoute] int taskId,
        [FromRoute] int unitId,
        [FromBody] CreateReminderUnitRequest request)
    {
        var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
        int userId = int.Parse(userIdClaim);
    
        var reminder = await _reminderService.CreateReminderUnit(
            taskId,
            unitId,
            //request.Title,
            request.Message,
            userId // createdBy
        );

        if (reminder == null)
        {
            return NotFound(new ApiResponse<object>
            {
                Data = null,
                Message = "Không tìm thấy người đứng đầu phòng ban."
            });
        }
    
        return Ok(new ApiResponse<object>
        {
            Data = new
            {
                reminder.Reminderid,
                reminder.Message,
                reminder.Triggertime,
                reminder.Createdat,
                TaskId = reminder.Taskid,
                UnitHeadUserId = reminder.UserId
            },
            Message = "Tạo nhắc nhở cho phòng ban thành công."
        });
    }
    
}
