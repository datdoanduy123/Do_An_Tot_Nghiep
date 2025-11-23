using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Dtos.UploadFile;
using DocTask.Core.Paginations;

namespace DocTask.Core.Interfaces.Services
{
    public interface IUploadFileService
    {
        Task<UploadFileDto> UploadFileAsync(UploadFileRequest request, int? userId);
        Task<UploadFileDto?> GetFileByIdAsync(int fileId);
        Task<List<UploadFileDto>> GetFileByUserIdAsync(int userId);
        Task<PaginatedList<UploadFileDto>> GetFileByUserIdPaginatedAsync(int userId, PageOptionsRequest pageOptions);
        Task<Stream?> DownloadFileAsync(int fileId);
        Task<string?> GetFileDownloadLinkAsync(int fileId);
        Task<bool> DeleteFileAsync(int fileId, int userId);
    }
}