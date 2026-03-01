// namespace DocTask.Api.Controllers;

// [ApiController]
// [Route("/api/auto-assign")]
// public class AiTaskAssignmentController : ControllerBase
// {
//     private readonly IAutoAssignmentService _autoAssignmentService;

//     public AiTaskAssignmentController(IAutoAssignmentService autoAssignmentService)
//     {
//         _autoAssignmentService = autoAssignmentService;
//     }

//     [HttpPost]
//     [SwaggerOperation(Summary = "Đề xuất phân công cho một danh sách task dựa trên workload availability.")]
//     public async Task<IActionResult> ProposeAssignmentsAsync([FromBody] List<int> taskIds)
//     {
//         var result = await _autoAssignmentService.ProposeAssignmentsAsync(taskIds);
//         return Ok(new ApiResponse<List<AssignmentProposalDto>>
//         {
//             Data    = result,
//             Message = "Đề xuất phân công thành công"
//         });
//     }
// }