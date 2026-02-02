using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Repositories
{
    public interface IEmployeeClusterRepository
    {
        Task<EmployeeCluster?> GetByIdAsync(int id);
        Task<EmployeeCluster?> GetByUserIdAsync(int userId);
        Task<List<EmployeeCluster>> GetByClusterIdAsync(int clusterId);
        Task<List<EmployeeCluster>> GetAllAsync();
        Task<EmployeeCluster> CreateAsync(EmployeeCluster cluster);
        System.Threading.Tasks.Task DeleteAsync(int id);
        System.Threading.Tasks.Task DeleteAllAsync();
    }
}