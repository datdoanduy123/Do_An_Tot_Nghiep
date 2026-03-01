using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;

namespace DocTask.Service.Services;

/// <summary>
/// Service quản lý AiTaskRule — CRUD rule cấu hình AI sinh task.
/// Delegate trực tiếp xuống IAiTaskRuleRepository.
/// </summary>
public class AiTaskRuleService : IAiTaskRuleService
{
    private readonly IAiTaskRuleRepository _ruleRepository;

    public AiTaskRuleService(IAiTaskRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository;
    }

    /// <summary>
    /// Lấy toàn bộ rule — admin xem danh sách đầy đủ.
    /// </summary>
    public Task<List<AiTaskRule>> GetAllAsync()
        => _ruleRepository.GetAllAsync();

    /// <summary>
    /// Lấy rule active theo loại.
    /// </summary>
    public Task<List<AiTaskRule>> GetActiveByTypeAsync(string ruleType)
        => _ruleRepository.GetActiveByTypeAsync(ruleType);

    /// <summary>
    /// Lấy chi tiết 1 rule theo ID.
    /// </summary>
    public Task<AiTaskRule?> GetByIdAsync(int ruleId)
        => _ruleRepository.GetByIdAsync(ruleId);

    /// <summary>
    /// Tạo rule mới, tự gán CreatedAt = UtcNow.
    /// </summary>
    public Task<AiTaskRule> CreateAsync(AiTaskRule rule)
    {
        rule.CreatedAt = DateTime.UtcNow;
        rule.IsActive  = true;
        return _ruleRepository.CreateAsync(rule);
    }

    /// <summary>
    /// Cập nhật rule, tự gán UpdatedAt = UtcNow.
    /// </summary>
    public Task<AiTaskRule?> UpdateAsync(AiTaskRule rule)
    {
        rule.UpdatedAt = DateTime.UtcNow;
        return _ruleRepository.UpdateAsync(rule);
    }

    /// <summary>
    /// Xóa mềm: IsActive = false — rule vẫn còn trong DB.
    /// </summary>
    public Task<bool> DeactivateAsync(int ruleId)
        => _ruleRepository.DeactivateAsync(ruleId);

    /// <summary>
    /// Xóa cứng rule khỏi DB.
    /// </summary>
    public Task<bool> DeleteAsync(int ruleId)
        => _ruleRepository.DeleteAsync(ruleId);
}
