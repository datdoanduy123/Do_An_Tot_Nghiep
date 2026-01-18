using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Dtos.UploadFile;
using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using DocTask.Core.Paginations;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using DocTask.Core.Models;

namespace DocTask.Service.Services
{
    /// <summary>
    /// Service xử lý upload file lên Cloudinary
    /// Hỗ trợ upload, download, lấy thông tin file và xóa file
    /// </summary>
    public class UploadFileService : IUploadFileService
    {
        private readonly Cloudinary _cloudinary;
        private readonly CloudinarySettings _settings;
        private readonly IUploadFileRepository _uploadFileRepository;
        
        public UploadFileService(
            Cloudinary cloudinary,
            IOptions<CloudinarySettings> settings,
            IUploadFileRepository uploadFileRepository
        )
        {
            _cloudinary = cloudinary;
            _settings = settings.Value;
            _uploadFileRepository = uploadFileRepository;
        }
        
        /// <summary>
        /// Upload file lên Cloudinary
        /// </summary>
        /// <param name="request">Request chứa file cần upload</param>
        /// <param name="userId">ID của user upload (nullable)</param>
        /// <returns>UploadFileDto chứa thông tin file đã upload</returns>
        public async Task<UploadFileDto> UploadFileAsync(
            UploadFileRequest request, 
            int? userId
        )
        {
            var file = request.File;
            var fileName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{file.FileName}";
            var publicId = $"{_settings.Folder}/{fileName}";
            using var stream = file.OpenReadStream();
            
            // Upload lên Cloudinary - RawUploadParams hỗ trợ mọi loại file
            var uploadParams = new RawUploadParams
            {
                File = new FileDescription(fileName, stream),
                PublicId = publicId,
                Folder = _settings.Folder,
                UseFilename = true,
                UniqueFilename = false,
                Overwrite = false
            };
            
            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            if (uploadResult.Error != null)
            {
                throw new Exception($"Cloudinary upload failed: {uploadResult.Error.Message}");
            }
            
            // Lưu vào database
            var uploadFile = new Uploadfile
            {
                FileName = file.FileName,
                FilePath = uploadResult.SecureUrl.ToString(), // Cloudinary URL
                FileSize = file.Length,
                ContentType = file.ContentType,
                UploadedAt = DateTime.Now,
                UploadedBy = userId,
                PublicId = uploadResult.PublicId // Lưu PublicId để delete sau
            };
            
            await _uploadFileRepository.CreateAsync(uploadFile);
            
            // Map entity sang DTO
            return MapToDto(uploadFile);
        }
        
        /// <summary>
        /// Lấy thông tin file theo ID
        /// </summary>
        /// <param name="fileId">ID của file</param>
        /// <returns>UploadFileDto hoặc null nếu không tìm thấy</returns>
        public async Task<UploadFileDto?> GetFileByIdAsync(int fileId)
        {
            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null)
                return null;
            
            return MapToDto(file);
        }
        
        /// <summary>
        /// Lấy danh sách tất cả files của user
        /// </summary>
        /// <param name="userId">ID của user</param>
        /// <returns>Danh sách UploadFileDto</returns>
        public async Task<List<UploadFileDto>> GetFileByUserIdAsync(int userId)
        {
            var files = await _uploadFileRepository.GetByUserAsync(userId);
            return files.Select(MapToDto).ToList();
        }
        
        /// <summary>
        /// Lấy danh sách files của user có phân trang
        /// </summary>
        /// <param name="userId">ID của user</param>
        /// <param name="pageOptions">Tùy chọn phân trang</param>
        /// <returns>PaginatedList chứa UploadFileDto</returns>
        public async Task<PaginatedList<UploadFileDto>> GetFileByUserIdPaginatedAsync(
            int userId, 
            PageOptionsRequest pageOptions
        )
        {
            var paginatedFiles = await _uploadFileRepository.GetByUserPaginatedAsync(userId, pageOptions);
            
            var dtos = paginatedFiles.Items.Select(MapToDto).ToList();
            
            return new PaginatedList<UploadFileDto>(
                dtos,
                paginatedFiles.MetaData.TotalItems,
                paginatedFiles.MetaData.PageIndex,
                pageOptions.Size > 0 ? pageOptions.Size : 10
            );
        }
        
        /// <summary>
        /// Download file từ Cloudinary
        /// </summary>
        /// <param name="fileId">ID của file</param>
        /// <returns>Stream của file hoặc null nếu không tìm thấy</returns>
        public async Task<Stream?> DownloadFileAsync(int fileId)
        {
            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null)
                return null;
            
            // Download từ Cloudinary URL
            using var httpClient = new HttpClient();
            var response = await httpClient.GetAsync(file.FilePath);
            
            if (!response.IsSuccessStatusCode)
                return null;
            
            return await response.Content.ReadAsStreamAsync();
        }
        
        /// <summary>
        /// Lấy download link của file
        /// </summary>
        /// <param name="fileId">ID của file</param>
        /// <returns>URL download hoặc null nếu không tìm thấy</returns>
        public async Task<string?> GetFileDownloadLinkAsync(int fileId)
        {
            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null)
                return null;
            
            // Cloudinary URL là public URL, trả về trực tiếp
            return file.FilePath;
        }
        
        /// <summary>
        /// Xóa file khỏi Cloudinary và database
        /// </summary>
        /// <param name="fileId">ID của file cần xóa</param>
        /// <param name="userId">ID của user thực hiện xóa (để kiểm tra quyền)</param>
        /// <returns>true nếu xóa thành công, false nếu không tìm thấy file</returns>
        public async Task<bool> DeleteFileAsync(int fileId, int userId)
        {
            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null)
                return false;
            
            // Kiểm tra quyền: chỉ user đã upload mới được xóa
            if (file.UploadedBy.HasValue && file.UploadedBy.Value != userId)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa file này");
            }
            
            // Delete từ Cloudinary
            if (!string.IsNullOrEmpty(file.PublicId))
            {
                var deleteParams = new DeletionParams(file.PublicId);
                var result = await _cloudinary.DestroyAsync(deleteParams);
                
                if (result.Error != null)
                {
                    throw new Exception($"Cloudinary delete failed: {result.Error.Message}");
                }
            }
            
            // Delete từ database
            await _uploadFileRepository.DeleteAsync(fileId);
            return true;
        }
        
        /// <summary>
        /// Map entity Uploadfile sang DTO
        /// </summary>
        private UploadFileDto MapToDto(Uploadfile file)
        {
            return new UploadFileDto
            {
                FileId = file.FileId,
                FileName = file.FileName,
                FilePath = file.FilePath,
                FileSize = file.FileSize,
                ContentType = file.ContentType,
                UploadedAt = file.UploadedAt,
                UploadedBy = file.UploadedBy
            };
        }
    }
}