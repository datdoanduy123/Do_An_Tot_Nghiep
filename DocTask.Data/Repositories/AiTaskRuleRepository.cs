using System.Collections.Generic;
using System.Threading.Tasks;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocTask.Data.Repositories;

/// <summary>
/// Repository truy cập bảng ai_task_rule.
/// Cung cấp các phương thức đọc rule theo loại và CRUD cho admin.
/// </summary>
public class AiTaskRuleRepository : IAiTaskRuleRepository
{
    private readonly ApplicationDbContext _context;

    public AiTaskRuleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy danh sách rule active theo loại, sắp xếp theo SortOrder.
    /// </summary>
    public async Task<List<AiTaskRule>> GetActiveByTypeAsync(string ruleType)
    {
        return await _context.AiTaskRules
            .Where(r => r.RuleType == ruleType && r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ToListAsync();
    }

    /// <summary>
    /// Lấy toàn bộ rule (bao gồm inactive) — dùng cho admin.
    /// </summary>
    public async Task<List<AiTaskRule>> GetAllAsync()
    {
        return await _context.AiTaskRules
            .OrderBy(r => r.RuleType)
            .ThenBy(r => r.SortOrder)
            .ToListAsync();
    }

    /// <summary>
    /// Lấy rule theo ID.
    /// </summary>
    public async Task<AiTaskRule?> GetByIdAsync(int ruleId)
    {
        return await _context.AiTaskRules.FindAsync(ruleId);
    }

    /// <summary>
    /// Tạo rule mới.
    /// </summary>
    public async Task<AiTaskRule> CreateAsync(AiTaskRule rule)
    {
        rule.CreatedAt = DateTime.UtcNow;
        _context.AiTaskRules.Add(rule);
        await _context.SaveChangesAsync();
        return rule;
    }

    /// <summary>
    /// Cập nhật rule. Trả về null nếu không tìm thấy.
    /// </summary>
    public async Task<AiTaskRule?> UpdateAsync(AiTaskRule rule)
    {
        var existing = await _context.AiTaskRules.FindAsync(rule.RuleId);
        if (existing == null) return null;

        existing.RuleType       = rule.RuleType;
        existing.Keyword        = rule.Keyword;
        existing.SkillName      = rule.SkillName;
        existing.RequiredLevel  = rule.RequiredLevel;
        existing.Importance     = rule.Importance;
        existing.PhaseName      = rule.PhaseName;
        existing.PhaseRatio     = rule.PhaseRatio;
        existing.PhaseHours     = rule.PhaseHours;
        existing.SortOrder      = rule.SortOrder;
        existing.IsActive       = rule.IsActive;
        existing.UpdatedAt      = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return existing;
    }

    /// <summary>
    /// Xóa mềm: set IsActive = false thay vì xóa khỏi DB.
    /// </summary>
    public async Task<bool> DeactivateAsync(int ruleId)
    {
        var rule = await _context.AiTaskRules.FindAsync(ruleId);
        if (rule == null) return false;

        rule.IsActive  = false;
        rule.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Xóa cứng rule khỏi DB.
    /// </summary>
    public async Task<bool> DeleteAsync(int ruleId)
    {
        var rule = await _context.AiTaskRules.FindAsync(ruleId);
        if (rule == null) return false;

        _context.AiTaskRules.Remove(rule);
        await _context.SaveChangesAsync();
        return true;
    }
}
