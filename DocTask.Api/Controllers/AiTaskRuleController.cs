using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace DocTask.Api.Controllers;

/// <summary>
/// Controller quản lý rule cấu hình cho AI sinh task.
/// Admin có thể thêm/sửa/xóa rule mà không cần sửa code.
/// </summary>
[ApiController]
[Route("/api/ai-rules")]
public class AiTaskRuleController : ControllerBase
{
    private readonly IAiTaskRuleService _ruleService;

    public AiTaskRuleController(IAiTaskRuleService ruleService)
    {
        _ruleService = ruleService;
    }

    // GET /api/ai-rules
    // GET /api/ai-rules?ruleType=SkillKeyword
    [HttpGet]
    [SwaggerOperation(
        Summary = "Lấy danh sách rule AI",
        Description = "Lấy tất cả rule. Dùng query ?ruleType=SkillKeyword|DefaultPhase|ModuleKeywordSkill để lọc theo loại và chỉ lấy rule active.")]
    public async Task<IActionResult> GetAll([FromQuery] string? ruleType = null)
    {
        if (!string.IsNullOrWhiteSpace(ruleType))
        {
            // Lọc theo loại và chỉ lấy active
            var filtered = await _ruleService.GetActiveByTypeAsync(ruleType);
            return Ok(new ApiResponse<List<AiTaskRule>>
            {
                Data    = filtered,
                Message = $"Lấy rule loại '{ruleType}' thành công"
            });
        }

        // Lấy tất cả (kể cả inactive) để admin quản lý
        var all = await _ruleService.GetAllAsync();
        return Ok(new ApiResponse<List<AiTaskRule>>
        {
            Data    = all,
            Message = "Lấy danh sách rule thành công"
        });
    }

    // GET /api/ai-rules/{id}
    [HttpGet("{id:int}")]
    [SwaggerOperation(Summary = "Lấy chi tiết 1 rule theo ID")]
    public async Task<IActionResult> GetById(int id)
    {
        var rule = await _ruleService.GetByIdAsync(id);
        if (rule == null)
            return NotFound(new ApiResponse<object>
            {
                Message = $"Không tìm thấy rule ID = {id}"
            });

        return Ok(new ApiResponse<AiTaskRule>
        {
            Data    = rule,
            Message = "Lấy rule thành công"
        });
    }

    // POST /api/ai-rules
    [HttpPost]
    [SwaggerOperation(
        Summary = "Tạo rule mới",
        Description = "RuleType: SkillKeyword | DefaultPhase | ModuleKeywordSkill. Xem model AiTaskRule để biết các field cần truyền.")]
    public async Task<IActionResult> Create([FromBody] AiTaskRule rule)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Validate RuleType hợp lệ
        var validTypes = new[] { "SkillKeyword", "DefaultPhase", "ModuleKeywordSkill" };
        if (!validTypes.Contains(rule.RuleType))
            return BadRequest(new ApiResponse<object>
            {
                Message = $"RuleType không hợp lệ. Phải là một trong: {string.Join(", ", validTypes)}"
            });

        var created = await _ruleService.CreateAsync(rule);
        return CreatedAtAction(nameof(GetById), new { id = created.RuleId },
            new ApiResponse<AiTaskRule>
            {
                Data    = created,
                Message = "Tạo rule mới thành công"
            });
    }

    // PUT /api/ai-rules/{id}
    [HttpPut("{id:int}")]
    [SwaggerOperation(Summary = "Cập nhật rule theo ID")]
    public async Task<IActionResult> Update(int id, [FromBody] AiTaskRule rule)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        // Đảm bảo ID trong URL và body giống nhau
        rule.RuleId = id;

        var updated = await _ruleService.UpdateAsync(rule);
        if (updated == null)
            return NotFound(new ApiResponse<object>
            {
                Message = $"Không tìm thấy rule ID = {id}"
            });

        return Ok(new ApiResponse<AiTaskRule>
        {
            Data    = updated,
            Message = "Cập nhật rule thành công"
        });
    }

    // DELETE /api/ai-rules/{id}
    // Xóa mềm mặc định — dùng ?hard=true để xóa cứng
    [HttpDelete("{id:int}")]
    [SwaggerOperation(
        Summary = "Xóa rule theo ID",
        Description = "Mặc định xóa mềm (IsActive = false). Thêm ?hard=true để xóa cứng khỏi DB.")]
    public async Task<IActionResult> Delete(int id, [FromQuery] bool hard = false)
    {
        bool success;

        if (hard)
        {
            // Xóa cứng khỏi DB
            success = await _ruleService.DeleteAsync(id);
        }
        else
        {
            // Xóa mềm: IsActive = false
            success = await _ruleService.DeactivateAsync(id);
        }

        if (!success)
            return NotFound(new ApiResponse<object>
            {
                Message = $"Không tìm thấy rule ID = {id}"
            });

        var msg = hard ? "Đã xóa cứng rule" : "Đã tắt rule (xóa mềm). Dùng ?hard=true để xóa vĩnh viễn.";
        return Ok(new ApiResponse<object> { Message = msg });
    }
}
