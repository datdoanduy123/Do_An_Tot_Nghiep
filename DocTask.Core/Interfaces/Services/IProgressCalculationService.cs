using DocTask.Core.Dtos.Tasks;

namespace DocTask.Core.Interfaces.Services;

public interface IProgressCalculationService
{
    Task<ProgressCalculationResponse> CalculateTaskProgressAsync(int taskId);
}
