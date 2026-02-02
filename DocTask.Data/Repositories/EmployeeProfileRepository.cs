using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using DocTask.Data;
using Microsoft.EntityFrameworkCore;
namespace DocTask.Data.Repositories;

public class EmployeeProfileRepository : IEmployeeProfileRepository
{
    private readonly ApplicationDbContext _context;

    public EmployeeProfileRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeProfile?> GetByIdAsync(int id)
    {
        return await _context.EmployeeProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.EmployeeProfileId == id);
    }

    public async Task<EmployeeProfile?> GetByUserIdAsync(int userId)
    {
        return await _context.EmployeeProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task<List<EmployeeProfile>> GetAllAsync()
    {
        return await _context.EmployeeProfiles
            .Include(p => p.User)
            .Where(p => p.Status == "Active")  // Chỉ lấy active employees
            .ToListAsync();
    }

    public async Task<EmployeeProfile> CreateAsync(EmployeeProfile profile)
    {
        _context.EmployeeProfiles.Add(profile);
        await _context.SaveChangesAsync();
        return profile;
    }

    public async Task<EmployeeProfile> UpdateAsync(EmployeeProfile profile)
    {
        profile.LastUpdated = DateTime.Now;
        _context.EmployeeProfiles.Update(profile);
        await _context.SaveChangesAsync();
        return profile;
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        var profile = await GetByIdAsync(id);
        if (profile != null)
        {
            _context.EmployeeProfiles.Remove(profile);
            await _context.SaveChangesAsync();
        }
    }
}