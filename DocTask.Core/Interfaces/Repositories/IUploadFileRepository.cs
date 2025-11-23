using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Models;
using DocTask.Core.Paginations;

namespace DocTask.Core.Interfaces.Repositories
{
    public interface IUploadFileRepository
    {
        Task<Uploadfile> CreateAsync(Uploadfile uploadfile);
        Task<Uploadfile?> GetByIdAsync(int fileId);
        Task<List<Uploadfile>> GetByUserAsync(int userId);
        Task<Uploadfile?> GetByIdAndUserIdAsync(int fileId, int userId);
        Task<PaginatedList<Uploadfile>> GetByUserPaginatedAsync(int userId, Paginations.PageOptionsRequest pageOptions);
        Task<bool> DeleteAsync(int fileId);
        // Task<int> SaveFileMetadataAsync(Uploadfile fileMeta);
    }
}