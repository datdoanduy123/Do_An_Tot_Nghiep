using DocTask.Core.Dtos.Units;
using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Services;

public interface IUnitService
{
    Task<List<UnitDto>> GetAllUnitAsync();
    Task<List<UnitDto>> GetAssignableUnitsAsync(int fromUnitId);
    Task<UnitTaskDto?> CreateTaskForUnitAsync(CreateTaskForUnitRequest request, int assignerUserId);
    Task<UnitDto?> GetUserUnitAsync(int userId);
    Task<User?> GetUnitHeadAsync(int unitId);
}