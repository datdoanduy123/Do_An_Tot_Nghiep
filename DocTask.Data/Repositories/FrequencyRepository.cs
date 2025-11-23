// using DocTask.Core.Interfaces.Repositories;
// using DocTask.Core.Models;

// namespace DocTask.Data.Repositories;

// public class FrequencyRepository : IFrequencyRepository
// {
//     private readonly ApplicationDbContext _context;

//     public FrequencyRepository(ApplicationDbContext context)
//     {
//         _context = context;
//     }

//     public async Task<Frequency> CreateAsync(Frequency frequency)
//     {
//         await _context.AddAsync(frequency);
//         await _context.SaveChangesAsync();

//         return frequency;
//     }
// }
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocTask.Data.Repositories;

public class FrequencyRepository : IFrequencyRepository
{
    private readonly ApplicationDbContext _context;

    public FrequencyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Frequency> CreateAsync(Frequency frequency)
    {
        await _context.AddAsync(frequency);
        await _context.SaveChangesAsync();

        return frequency;
    }

    public async Task<Frequency?> GetByIdAsync(int frequencyId)
    {
        return await _context.Frequencies
            .Include(f => f.FrequencyDetails) // Include related frequency details if needed
            .FirstOrDefaultAsync(f => f.FrequencyId == frequencyId);
    }

    public async Task<Frequency> UpdateFreAsync(Frequency frequency)
    {
        _context.Frequencies.Update(frequency);
        await _context.SaveChangesAsync();

        return frequency;
    }

    public async Task<Frequency> UpdateFreRepoAsync(Frequency frequency)
    {
        // Alternative update method - you can choose which one to use
        var existingFrequency = await _context.Frequencies
            .FirstOrDefaultAsync(f => f.FrequencyId == frequency.FrequencyId);

        if (existingFrequency != null)
        {
            existingFrequency.FrequencyType = frequency.FrequencyType;
            existingFrequency.IntervalValue = frequency.IntervalValue;

            await _context.SaveChangesAsync();
        }

        return existingFrequency ?? frequency;
    }

    public async Task<bool> DeleteAsync(int frequencyId)
    {
        var frequency = await _context.Frequencies
            .FirstOrDefaultAsync(f => f.FrequencyId == frequencyId);

        if (frequency == null)
            return false;

        _context.Frequencies.Remove(frequency);
        await _context.SaveChangesAsync();

        return true;
    }
}