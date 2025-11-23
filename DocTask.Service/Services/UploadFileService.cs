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
using Amazon.S3;
using Microsoft.Extensions.Options;
using Amazon.S3.Model;
using System.Web;

namespace DocTask.Service.Services
{
    public class UploadFileService : IUploadFileService
    {
        private readonly IUploadFileRepository _uploadFileRepository;
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly string _endpoint;

        public UploadFileService(
            IAmazonS3 s3Client,
            IOptions<MinioSettings> settings,
            IUploadFileRepository uploadFileRepository
        )
        {
            _s3Client = s3Client ?? throw new ArgumentNullException(nameof(s3Client));
            _bucketName = settings.Value.BucketName?.Trim().ToLower() ?? throw new ArgumentNullException("BucketName is null");
            _uploadFileRepository = uploadFileRepository ?? throw new ArgumentNullException(nameof(uploadFileRepository));
            _endpoint = settings.Value.ServiceURL?.TrimEnd('/') ?? "http://localhost:9000";
        }

        private string SanitizeKey(string fileName)
        {
            // Thay dấu cách bằng _ và loại bỏ ký tự không hợp lệ
            var safeName = fileName.Trim().Replace(" ", "_");
            return safeName;
        }

        public async Task<UploadFileDto> UploadFileAsync(UploadFileRequest request, int? userId)
        {
            if (userId == null)
            {
                throw new ArgumentNullException(nameof(userId), "User ID cannot be null");
            }

            if (request.File == null || request.File.Length == 0)
            {
                throw new ArgumentException("No file provided");
            }

            const long maxFileSize = 10 * 1024 * 1024; // 10 MB
            if (request.File.Length > maxFileSize)
            {
                throw new ArgumentException($"File size exceeds the maximum limit of {maxFileSize / (1024 * 1024)} MB");
            }

            // Kiểm tra định dạng file
            List<string> validExtensions = new List<string>()
            {
                ".jpg", ".png", ".gif", // Image
                ".pdf", ".txt",         // Documents
                ".doc", ".docx",        // Word
                ".xls", ".xlsx",        // Excel
            };

            var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            if (!validExtensions.Contains(fileExtension))
            {
                throw new ArgumentException($@"
                    Extension is not valid({string.Join(',', validExtensions)})
                    File Error: {request.File.FileName}
                    ");
            }

            // Name
            // string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            // string cleanFileName = Path.GetFileNameWithoutExtension(request.File.FileName);
            // string nameExtension = Path.GetExtension(request.File.FileName);
            // var uniqueFileName = $"{cleanFileName}_user{userId}_at_{timestamp}{nameExtension}";

            var uniqueFileName = $"{Guid.NewGuid()}_{SanitizeKey(request.File.FileName)}";

            // Kiểm tra bucket tồn tại
            var buckets = await _s3Client.ListBucketsAsync();
            if (!buckets.Buckets.Any(b => b.BucketName == _bucketName))
                throw new Exception($"Bucket '{_bucketName}' không tồn tại hoặc client không nhìn thấy!");

            using var stream = request.File.OpenReadStream();
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = uniqueFileName,
                InputStream = stream,
                ContentType = request.File.ContentType
            };

            await _s3Client.PutObjectAsync(putRequest);

            var uploadFile = new Uploadfile
            {
                FileName = request.File.FileName,
                FilePath = uniqueFileName, // chỉ lưu key an toàn
                UploadedBy = userId,
                UploadedAt = DateTime.UtcNow
            };

            var savedFile = await _uploadFileRepository.CreateAsync(uploadFile);

            return new UploadFileDto
            {
                FileId = savedFile.FileId,
                FileName = savedFile.FileName,
                FilePath = $"{_endpoint}/{_bucketName}/{HttpUtility.UrlEncode(savedFile.FilePath)}",
                UploadedBy = savedFile.UploadedBy,
                UploadedAt = savedFile.UploadedAt,
                FileSize = request.File.Length,
                ContentType = request.File.ContentType,
            };
        }

        public async Task<UploadFileDto?> GetFileByIdAsync(int fileId)
        {
            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null)
            {
                return null;
            }

            var fileInfo = new FileInfo(file.FilePath);

            return new UploadFileDto
            {
                FileId = file.FileId,
                FileName = file.FileName,
                FilePath = $"{_endpoint}/{_bucketName}/{HttpUtility.UrlEncode(file.FilePath)}",
                UploadedBy = file.UploadedBy,
                UploadedAt = file.UploadedAt,
                FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                ContentType = GetContentType(file.FileName),
            };
        }

        public async Task<List<UploadFileDto>> GetFileByUserIdAsync(int userId)
        {
            var files = await _uploadFileRepository.GetByUserAsync(userId);
            if (files == null)
            {
                return null;
            }

            return files.Select(f =>
            {
                var fileInfo = new FileInfo(f.FilePath);

                return new UploadFileDto
                {
                    FileId = f.FileId,
                    FileName = f.FileName,
                    FilePath = $"{_endpoint}/{_bucketName}/{HttpUtility.UrlEncode(f.FilePath)}",
                    UploadedBy = f.UploadedBy,
                    UploadedAt = f.UploadedAt,
                    FileSize = fileInfo.Exists ? fileInfo.Length : 0,
                    ContentType = GetContentType(f.FileName),
                };
            }).ToList();
        }

        public async Task<PaginatedList<UploadFileDto>> GetFileByUserIdPaginatedAsync(int userId, PageOptionsRequest pageOptions)
        {
            var allFiles = await GetFileByUserIdAsync(userId);
            var items = allFiles
                .Skip((pageOptions.Page - 1) * pageOptions.Size)
                .Take(pageOptions.Size)
                .ToList();

            return new PaginatedList<UploadFileDto>(
                items,
                allFiles.Count,
                pageOptions.Page,
                pageOptions.Size
            );
        }

        public async Task<Stream?> DownloadFileAsync(int fileId)
        {
            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null || string.IsNullOrEmpty(file.FilePath))
            {
                throw new FileNotFoundException($"File not found.");
            }

            var getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = file.FilePath,
            };

            var response = await _s3Client.GetObjectAsync(getRequest);
            return response.ResponseStream;
        }

        public async Task<string?> GetFileDownloadLinkAsync(int fileId)
        {
            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null)
            {
                throw new FileNotFoundException($"File not found.");
            }
                
            var getRequest = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = file.FilePath,
                Expires = DateTime.UtcNow.AddMinutes(15) // Link hợp lệ trong 15 phút
            };

            getRequest.ResponseHeaderOverrides.ContentDisposition = $"attachment; filename=\"{file.FileName}\"";

            var url = _s3Client.GetPreSignedURL(getRequest);
            return url;
        }

        public async Task<bool> DeleteFileAsync(int fileId, int userId)
        {
            var file = await _uploadFileRepository.GetByIdAsync(fileId);
            if (file == null)
            {
                return false;
            }

            var deleteRequest = new DeleteObjectRequest
            {   
                BucketName = _bucketName,
                Key = file.FilePath
            };

            var response = await _s3Client.DeleteObjectAsync(deleteRequest);
            if (response.HttpStatusCode == System.Net.HttpStatusCode.NoContent)
            {
                await _uploadFileRepository.DeleteAsync(fileId);
                return true;
            }

            return false;
        }

        private static string GetContentType(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".pdf" => "application/pdf",
                ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".txt" => "text/plain",
                _ => "application/octet-stream",
            };
        }
    }
}