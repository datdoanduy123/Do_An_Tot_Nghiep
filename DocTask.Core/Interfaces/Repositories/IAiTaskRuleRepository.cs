using System.Collections.Generic;
using System.Threading.Tasks;
using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Repositories;

/// <summary>
/// Interface repository cho AiTaskRule — truy vấn rule cấu hình AI sinh task từ DB.
/// </summary>
public interface IAiTaskRuleRepository
{
    /// <summary>
    /// Lấy tất cả rule đang active theo loại.
    /// </summary>
    /// <param name="ruleType">SkillKeyword | DefaultPhase | ModuleKeywordSkill</param>
    Task<List<AiTaskRule>> GetActiveByTypeAsync(string ruleType);

    /// <summary>
    /// Lấy toàn bộ rule (kể cả inactive) — dùng cho admin quản lý.
    /// </summary>
    Task<List<AiTaskRule>> GetAllAsync();

    /// <summary>
    /// Lấy rule theo ID.
    /// </summary>
    Task<AiTaskRule?> GetByIdAsync(int ruleId);

    /// <summary>
    /// Tạo rule mới.
    /// </summary>
    Task<AiTaskRule> CreateAsync(AiTaskRule rule);

    /// <summary>
    /// Cập nhật rule.
    /// </summary>
    Task<AiTaskRule?> UpdateAsync(AiTaskRule rule);

    /// <summary>
    /// Xóa mềm: set IsActive = false.
    /// </summary>
    Task<bool> DeactivateAsync(int ruleId);

    /// <summary>
    /// Xóa cứng rule.
    /// </summary>
    Task<bool> DeleteAsync(int ruleId);
}
