using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Repositories;

public interface IFrequencyDetailRepository
{
    Task<FrequencyDetail> CreateAsync(FrequencyDetail frequencyDetail);

    Task<bool> DeleteByFrequencyIdAsync(int frequencyId);
}