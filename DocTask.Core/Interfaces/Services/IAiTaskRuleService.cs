using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Services;

/// <summary>
/// Interface service quản lý AiTaskRule — CRUD rule cho AI sinh task.
/// </summary>
public interface IAiTaskRuleService
{
    /// <summary>
    /// Lấy toàn bộ rule (kể cả inactive) — dành cho admin xem danh sách.
    /// </summary>
    Task<List<AiTaskRule>> GetAllAsync();

    /// <summary>
    /// Lấy rule active theo loại: SkillKeyword | DefaultPhase | ModuleKeywordSkill.
    /// </summary>
    Task<List<AiTaskRule>> GetActiveByTypeAsync(string ruleType);

    /// <summary>
    /// Lấy chi tiết 1 rule theo ID.
    /// </summary>
    Task<AiTaskRule?> GetByIdAsync(int ruleId);

    /// <summary>
    /// Tạo rule mới.
    /// </summary>
    Task<AiTaskRule> CreateAsync(AiTaskRule rule);

    /// <summary>
    /// Cập nhật thông tin rule.
    /// </summary>
    Task<AiTaskRule?> UpdateAsync(AiTaskRule rule);

    /// <summary>
    /// Xóa mềm: set IsActive = false.
    /// </summary>
    Task<bool> DeactivateAsync(int ruleId);

    /// <summary>
    /// Xóa cứng rule khỏi DB.
    /// </summary>
    Task<bool> DeleteAsync(int ruleId);
}
