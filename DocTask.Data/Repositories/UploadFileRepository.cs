using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Models;
using Microsoft.EntityFrameworkCore;
using DocTask.Core.Paginations;

namespace DocTask.Data.Repositories
{
    public class UploadFileRepository : IUploadFileRepository
    {
        private readonly ApplicationDbContext _context;

        public UploadFileRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Uploadfile> CreateAsync(Uploadfile uploadfile)
        {
            _context.Uploadfiles.Add(uploadfile);
            await _context.SaveChangesAsync();
            return uploadfile;
        }

        public async Task<Uploadfile?> GetByIdAsync(int fileId)
        {
            return await _context.Uploadfiles
                .Include(u => u.UploadedByNavigation)
                .FirstOrDefaultAsync(u => u.FileId == fileId);
        }

        public async Task<List<Uploadfile>> GetByUserAsync(int userId)
        {
            return await _context.Uploadfiles
                .Where(u => u.UploadedBy == userId)
                .OrderByDescending(u => u.UploadedAt)
                .ToListAsync();
        }

        public async Task<Uploadfile?> GetByIdAndUserIdAsync(int fileId, int userId)
        {
            return await _context.Uploadfiles
                .FirstOrDefaultAsync(u => u.FileId == fileId && u.UploadedBy == userId);
        }

        public async Task<PaginatedList<Uploadfile>> GetByUserPaginatedAsync(int userId, PageOptionsRequest pageOptions)
        {
            var query = _context.Uploadfiles
                .Where(u => u.UploadedBy == userId)
                .OrderByDescending(u => u.UploadedAt)
                .AsNoTracking();

            return await query.ToPaginatedListAsync(pageOptions);
        }

        public async Task<bool> DeleteAsync(int fileId)
        {
            var file = await _context.Uploadfiles.FindAsync(fileId);
            if (file == null)
            {
                return false;
            }

            _context.Uploadfiles.Remove(file);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}