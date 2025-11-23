using DocTask.Core.Models;

namespace DocTask.Core.Interfaces.Repositories;

public interface IFrequencyRepository
{
    Task<Frequency> CreateAsync(Frequency frequency);

    Task<Frequency?> GetByIdAsync(int frequencyId);
    Task<Frequency> UpdateFreAsync(Frequency frequency);
}