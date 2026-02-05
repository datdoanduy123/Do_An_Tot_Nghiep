using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;
namespace DocTask.Data.Repositories;

public class EmployeeClusterRepository : IEmployeeClusterRepository
{
    private readonly ApplicationDbContext _context;

    public EmployeeClusterRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<EmployeeCluster?> GetByIdAsync(int id)
    {
        return await _context.EmployeeClusters
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.EmployeeClusterId == id);
    }

    public async Task<EmployeeCluster?> GetByUserIdAsync(int userId)
    {
        return await _context.EmployeeClusters
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    public async Task<List<EmployeeCluster>> GetByClusterIdAsync(int clusterId)
    {
        return await _context.EmployeeClusters
            .Include(c => c.User)
            .Where(c => c.ClusterId == clusterId)
            .ToListAsync();
    }

    public async Task<List<EmployeeCluster>> GetAllAsync()
    {
        return await _context.EmployeeClusters
            .Include(c => c.User)
            .ToListAsync();
    }

    public async Task<EmployeeCluster> CreateAsync(EmployeeCluster cluster)
    {
        _context.EmployeeClusters.Add(cluster);
        await _context.SaveChangesAsync();
        return cluster;
    }

    public async System.Threading.Tasks.Task DeleteAsync(int id)
    {
        var cluster = await GetByIdAsync(id);
        if (cluster != null)
        {
            _context.EmployeeClusters.Remove(cluster);
            await _context.SaveChangesAsync();
        }
    }

    public async System.Threading.Tasks.Task DeleteAllAsync()
    {
        var allClusters = await _context.EmployeeClusters.ToListAsync();
        _context.EmployeeClusters.RemoveRange(allClusters);
        await _context.SaveChangesAsync();
    }
}