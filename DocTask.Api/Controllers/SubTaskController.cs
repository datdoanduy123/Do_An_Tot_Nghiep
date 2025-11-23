using DocTask.Core.Dtos.SubTasks;
using DocTask.Core.Dtos.Units;
using DocTask.Core.Dtos.Users;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Exceptions;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Paginations;
using DocTask.Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DocTask.Api.Controllers
{
    [ApiController]
    [Route("api/v1/subtask")]
    public class SubTaskController : ControllerBase
    {
        private readonly ISubTaskService _subTaskService;

        public SubTaskController(ISubTaskService subTaskService, IUserRepository userRepository, IFrequencyRepository frequencyRepository, IFrequencyDetailRepository frequencyDetailRepository)
        {
            _subTaskService = subTaskService;
        }

        // POST: api/v1/subtask/{parentTaskId} - Tạo subtask mới
        [HttpPost("{parentTaskId:int}")]
        [SwaggerOperation(Summary = "Tạo một subtask mới cho một công việc cha cụ thể.")]
        public async Task<IActionResult> CreateSubTask(int parentTaskId, [FromBody] CreateSubTaskRequest request)
        {
            // Get current user ID for assignerId
            var userId = GetUserIdFromHttpContext();

            var subTaskDto = await _subTaskService.CreateAsync(parentTaskId, request, userId.Value);

            return Ok(new ApiResponse<SubTaskDto>
            {
                Success = true,
                Data = subTaskDto,
                Message = "Tạo subtask thành công"
            });
        }
        
        [HttpGet("by-parent-task/{parentTaskId:int}")]
        [SwaggerOperation(Summary = "Lấy danh sách các subtask của một công việc cha cụ thể, có phân trang và tìm kiếm.")]
        public async Task<IActionResult> GetSubTasks(
            [FromRoute] int parentTaskId,
            [FromQuery] string? key,
            [FromQuery] PageOptionsRequest pageOptions
            )
        {
            var userId = GetUserIdFromHttpContext();
            
            // Get by parent task ID with pagination
            var subtasks = await _subTaskService.GetAllByParentIdAsync(userId.Value, parentTaskId, pageOptions, key);
            return Ok(new ApiResponse<PaginatedList<SubTaskDto>>
            {
                Success = true,
                Data = subtasks,
                Message = "Lấy danh sách subtasks thành công"
            });
        }

        [HttpGet("{subTaskId}")]
        [SwaggerOperation(Summary = "Lấy Chi tiết 1 công việc con")]
        public async Task<IActionResult> GetSubTaskById([FromRoute] int subTaskId)
        {
            var subtask = await _subTaskService.GetSubTaskByIdAsync(subTaskId);
            return Ok(new ApiResponse<SubTaskDto>
            {
                Success = true,
                Data = subtask,
                Message = "Cập nhật subtask thành công"
            });
        }

        [HttpGet("{subTaskId}/unit-detail")]
        [SwaggerOperation(Summary = "Lấy chi tiết công việc được giao cho unit với thông tin người đứng đầu")]
        public async Task<IActionResult> GetSubTaskUnitDetail([FromRoute] int subTaskId)
        {
            var subTask = await _subTaskService.GetSubTaskUnitByIdAsync(subTaskId);

            return Ok(new ApiResponse<SubTaskDto>
            {
                Success = true,
                Data = subTask,
                Message = "Lấy chi tiết task giao cho unit thành công"
            });
        }

        // GET: api/v1/subtask/assignable-users - Get subordinates and peers for task assignment
        [HttpGet("assignable-users")]
        [SwaggerOperation(Summary = "Lấy danh sách người dùng để giao việc.")]
        public async Task<IActionResult> GetAssignableUsers()
        {
            var userId = GetUserIdFromHttpContext();
            if (userId == null)
            {
                throw new UnauthorizedException("Không thể xác thực người dùng");
            }

            var result = await _subTaskService.GetAssignableUsers(userId.Value);
            return Ok(new ApiResponse<AssignableUsersResponseDto>
            {
                Success = true,
                Data = result,
                Message = "Lấy danh sách người dùng để giao việc thành công"
            });
        }
        
        [HttpGet("assignable-units")]
        [SwaggerOperation(Summary = "Lấy danh sách đơn vị để giao việc.")]
        public async Task<IActionResult> GetAssignableUnits()
        {
            var userId = GetUserIdFromHttpContext();
            if (userId == null)
            {
                throw new UnauthorizedException("Không thể xác thực người dùng");
            }

            var result = await _subTaskService.GetAssignableUnits(userId.Value);

            return Ok(new ApiResponse<AssignableUnitsResponseDto>
            {
                Success = true,
                Data = result,
                Message = "Lấy danh sách đơn vị để giao việc thành công"
            });
        }

        [HttpGet("assigned")]
        [SwaggerOperation(Summary = "Lấy danh sách subtask được giao cho người dùng hiện tại, phân trang và tìm kiếm.")]
        public async Task<IActionResult> GetMySubTasks([FromQuery] PageOptionsRequest pageOptions, [FromQuery] string? key)
        {
            var userId = GetUserIdFromHttpContext();
            if (userId == null)
            {
                throw new UnauthorizedException("Không thể xác thực người dùng");
            }

            var subtasks = await _subTaskService.GetByAssignedUserIdPaginatedAsync(userId.Value, key, pageOptions);

            return Ok(new ApiResponse<PaginatedList<SubTaskDto>>
            {
                Success = true,
                Data = subtasks,
                Message = "Lấy danh sách subtask của bạn thành công"
            });
        }


        // PUT: api/v1/subtask/{parentTaskId}?subTaskId=33

        [HttpPut] 
        [SwaggerOperation(Summary = "Cập nhật một subtask theo ID.")]
        public async Task<IActionResult> UpdateSubTask([FromQuery] int subTaskId, [FromBody] UpdateSubTaskRequest request)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            var userId = int.Parse(userIdClaim);
            
            var updatedSubTask = await _subTaskService.UpdateSubtask(userId, subTaskId, request);
            if (updatedSubTask == null)
            {
                throw new NotFoundException("Subtask không tồn tại");
            }

            return Ok(new ApiResponse<SubTaskDto>
            {
                Success = true,
                Data = updatedSubTask,
                Message = "Cập nhật subtask thành công"
            });
        }

        // DELETE: api/v1/subtask?subTaskId=33
        [HttpDelete]
        [SwaggerOperation(Summary = "Xoá một subtask theo ID.")]
        public async Task<IActionResult> DeleteSubTask([FromQuery] int subTaskId)
        {
            //phân quyền
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                throw new UnauthorizedException("Không thể xác thực người dùng");
            }
            int userId = int.Parse(userIdClaim);

            //Check điều kiện
            if (subTaskId <= 0)
            {
                throw new BadRequestException("SubTaskId không hợp lệ");
            }
            var success = await _subTaskService.DeleteAsync(subTaskId, userId);
            if (!success)
            {
                throw new NotFoundException("Subtask không tồn tại");
            }
            //Xóa thành công
            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Data = true,
                Message = "Xóa subtask thành công"
            });
        }
        private int? GetUserIdFromHttpContext()
        {
            var idClaim = HttpContext.User.FindFirst("id");
            if (idClaim == null) return null;
            if (int.TryParse(idClaim.Value, out var id)) return id;
            return null;
        }


        [HttpPatch("statusparent/{taskId}")]
        [SwaggerOperation(Summary = "Cập nhật trạng thái tiến độ cho parent task(pending, in_progress, completed)")]
        public async Task<IActionResult> ChangeStatusParentTask(int taskId, [FromBody] string status)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                throw new UnauthorizedException("Không thể xác thực người dùng");
            }
            int userId = int.Parse(userIdClaim);

            var result = await _subTaskService.ChangeParentTaskStatusAsync(taskId, userId, status);
            if (!result)
                return BadRequest(new ApiResponse<string> { Success = false, Error = "Cập nhật trạng thái thất bại hoặc không hợp lệ." });

            return Ok(new ApiResponse<string>
            {
                Success = true,
                Message = $"SubTask {taskId} đã được cập nhật thành: {status.ToLower()}."
            });
        }


        [HttpPatch("status/{taskId}")]
        [SwaggerOperation(Summary = "Cập nhật trạng thái tiến độ cho subtask (pending, in_progress, completed)")]
        public async Task<IActionResult> ChangeStatusSubTask(int taskId, [FromBody] string status)
        {

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id")?.Value;
            if (string.IsNullOrWhiteSpace(userIdClaim))
            {
                throw new UnauthorizedException("Không thể xác thực người dùng");
            }
            int userId = int.Parse(userIdClaim);

            var result = await _subTaskService.ChangeSubTaskStatusAsync(taskId, userId, status);
            if (!result)
                return BadRequest(new ApiResponse<string> { Success = false, Error = "Cập nhật trạng thái thất bại hoặc không hợp lệ." });

            return Ok(new ApiResponse<string>
            {
                Success = true,
                Message = $"SubTask {taskId} đã được cập nhật thành: {status.ToLower()}."
            });
        }
        //[HttpGet("{taskId}/assigned-users")]

        [HttpGet("{taskId}/assigned-users-units")]
        [SwaggerOperation(Summary = "Lấy tất cả user được giao trong 1 subtask")]
        public async Task<IActionResult> GetAllUserTask([FromRoute] int taskId)
        {
            var users = await _subTaskService.GetAssignedUsersAsync(taskId);
            return Ok(new ApiResponse<List<object>>
            {
                Success = true,
                Data = users,
                Message = "Lấy danh sách user được giao thành công"
            });
        }
    }
}