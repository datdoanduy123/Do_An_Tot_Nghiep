// using DocTask.Core.Interfaces.Repositories;
// using DocTask.Core.Models;

// namespace DocTask.Data.Repositories;

// public class FrequencyDetailRepository : IFrequencyDetailRepository
// {
//     private readonly ApplicationDbContext _context;

//     public FrequencyDetailRepository(ApplicationDbContext context)
//     {
//         _context = context;
//     }

//     public async Task<FrequencyDetail> CreateAsync(FrequencyDetail frequencyDetail)
//     {
//         await _context.FrequencyDetails.AddAsync(frequencyDetail);
//         await _context.SaveChangesAsync();
//         return frequencyDetail;
//     }
// }

using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DocTask.Data.Repositories;

public class FrequencyDetailRepository : IFrequencyDetailRepository
{
    private readonly ApplicationDbContext _context;

    public FrequencyDetailRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    // Method chính cần cho update
    public async Task<FrequencyDetail> CreateAsync(FrequencyDetail frequencyDetail)
    {
        await _context.FrequencyDetails.AddAsync(frequencyDetail);
        await _context.SaveChangesAsync();
        return frequencyDetail;
    }

    // Method chính cần cho update  
    public async Task<bool> DeleteByFrequencyIdAsync(int frequencyId)
    {
        var frequencyDetails = await _context.FrequencyDetails
            .Where(fd => fd.FrequencyId == frequencyId)
            .ToListAsync();

        if (frequencyDetails.Any())
        {
            _context.FrequencyDetails.RemoveRange(frequencyDetails);
            await _context.SaveChangesAsync();
        }

        return true;
    }

    // Các method khác implement đơn giản
    public async Task<FrequencyDetail?> GetByIdAsync(int id)
    {
        return await _context.FrequencyDetails.FirstOrDefaultAsync(fd => fd.Id == id);
    }

    public async Task<List<FrequencyDetail>> GetByFrequencyIdAsync(int frequencyId)
    {
        return await _context.FrequencyDetails.Where(fd => fd.FrequencyId == frequencyId).ToListAsync();
    }

    public async Task<FrequencyDetail> UpdateAsync(FrequencyDetail frequencyDetail)
    {
        _context.FrequencyDetails.Update(frequencyDetail);
        await _context.SaveChangesAsync();
        return frequencyDetail;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var item = await _context.FrequencyDetails.FirstOrDefaultAsync(fd => fd.Id == id);
        if (item != null)
        {
            _context.FrequencyDetails.Remove(item);
            await _context.SaveChangesAsync();
        }
        return true;
    }
}