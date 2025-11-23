using DocTask.Core.Models;
using Task = System.Threading.Tasks.Task;

namespace DocTask.Core.Interfaces.Repositories;

public interface IUnitRepository
{
    Task<List<Unit>> GetAllUnitsAsync();
    Task<List<User>> GetUsersByUnitIdAsync(int unitId);
    Task<string?> GetUserUnitNameByIdAsync(int userId);
    Task<Unit?> GetUnitByIdAsync(int unitId);
    Task<List<Unit>> GetAssignableUnitsAsync(int fromUnitId);
    Task<List<Unit>> GetChildUnitsAsync(int parentUnitId);
    Task<bool> CanAssignToUnitAsync(int fromUnitId, int targetUnitId);
    Task<bool> IsChildUnitAsync(int parentUnitId, int childUnitId);
    Task<List<Unit>> GetParentUnitsAsync(int childUnitId);
    Task <List<Unit>> GetSubUnitsByParentUnitIdAsync(int parentUnitId);
    Task<User?> GetLeaderOfUnit(int unitId);
    Task<List<int>> GetLeaderIdsOfAssignedUnitsByTaskId(int taskId);
}