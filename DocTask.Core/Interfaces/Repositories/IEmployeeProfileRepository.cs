using DocTask.Core.Models;
namespace DocTask.Core.Interfaces.Repositories;

public interface IEmployeeProfileRepository
{
    Task<EmployeeProfile?> GetByIdAsync(int id);
    Task<EmployeeProfile?> GetByUserIdAsync(int userId);
    Task<List<EmployeeProfile>> GetAllAsync();
    Task<EmployeeProfile> CreateAsync(EmployeeProfile profile);
    Task<EmployeeProfile> UpdateAsync(EmployeeProfile profile);
    System.Threading.Tasks.Task DeleteAsync(int id);
}