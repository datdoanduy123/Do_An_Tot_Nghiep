using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DocTask.Core.Dtos.UploadFile;
using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Interfaces.Services;
using DocTask.Core.Models;
using Microsoft.AspNetCore.Mvc;
using DocTask.Core.Paginations;
using Swashbuckle.AspNetCore.Annotations;


namespace DockTask.Api.Controllers
{
    [ApiController]
    [Route("/api/v1/file")]
    public class UploadFileController : ControllerBase
    {
        private readonly IUploadFileService _uploadFileService;

        public UploadFileController(IUploadFileService uploadFileService)
        {
            _uploadFileService = uploadFileService;
        }

        // POST: api/file/upload
        [HttpPost("upload")]
        public async Task<IActionResult> UploadFile([FromForm] UploadFileRequest request)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized(new ApiResponse<string>
                {
                    Success = false,
                    Error = "User not authenticated"
                });
            }

            var result = await _uploadFileService.UploadFileAsync(request, userId);
            var response = new ApiResponse<UploadFileDto>
            {
                Success = true,
                Data = result,
                Message = "File uploaded successfully"
            };
            return Ok(response);
        }

        // GET: api/file/get/{fileId}
        // [HttpGet("get/{fileId}")]
        // public async Task<IActionResult> GetFile(int fileId)
        // {
        //     var file = await _uploadFileService.GetFileByIdAsync(fileId);
        //     if (file == null)
        //     {
        //         return NotFound(new ApiResponse<string>
        //         {
        //             Success = false,
        //             Error = "File not found"
        //         });
        //     }

        //     return Ok(new ApiResponse<UploadFileDto>
        //     {
        //         Success = true,
        //         Data = file
        //     });
        // }

        // GET: api/file/user
        [HttpGet("user")]
        public async Task<IActionResult> GetUserFile([FromQuery] int page = 1, [FromQuery] int size = 10)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized(new ApiResponse<string>
                {
                    Success = false,
                    Error = "User not authenticated"
                });
            }

            var pageOptions = new PageOptionsRequest { Page = page, Size = size };
            var files = await _uploadFileService.GetFileByUserIdPaginatedAsync(userId, pageOptions);

            return Ok(new ApiResponse<PaginatedList<UploadFileDto>>
            {
                Success = true,
                Data = files,
                Message = "Files fetched successfully"
            });
        }

        // GET: api/file/download/{fileId}
        [HttpGet("download/{fileId}")]
        public async Task<IActionResult> DownloadFile([FromRoute] int fileId)
        {
            var file = await _uploadFileService.GetFileByIdAsync(fileId);
            if (file == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "File does not exist."
                });
            }

            var fileContent = await _uploadFileService.DownloadFileAsync(fileId);
            if (fileContent == null)
            {
                return NotFound(new ApiResponse<object>
                {
                    Success = false,
                    Error = "File not found"
                });
            }

            var fileName = file.FileName ?? "file.dat"; //fallback
            var contentType = string.IsNullOrEmpty(file.ContentType) ? "application/octet-stream" : file.ContentType;

            // Encode tên file Unicode để tránh lỗi header
            var encodedFileName = Uri.EscapeDataString(fileName);

            return File(fileContent, contentType, encodedFileName);
        }

        // GET: api/file/download/link/{fileId}
        [HttpGet("download/link/{fileId}")]
        [SwaggerOperation(Summary = "Lấy link tải xuống một file theo ID.")]
        public async Task<IActionResult> DownloadLink([FromRoute] int fileId)
        {
            var url = await _uploadFileService.GetFileDownloadLinkAsync(fileId);
            if (url == null)
            {
                return NotFound();
            }

            return Ok(new { downloadUrl = url });
        }

        // DELETE: api/file/delete/{fileId}
        [HttpDelete("delete/{fileId}")]
        public async Task<IActionResult> DeleteFile([FromRoute] int fileId)
        {
            var userIdString = User.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
            {
                return Unauthorized(new ApiResponse<string>
                {
                    Success = false,
                    Error = "User not authenticated"
                });
            }

            var result = await _uploadFileService.DeleteFileAsync(fileId, userId);
            if (!result)
            {
                return NotFound(new ApiResponse<bool>
                {
                    Success = false,
                    Error = "File not found"
                });
            }

            return Ok(new ApiResponse<bool>
            {
                Success = true,
                Data = true,
                Message = "File deleted successfully!"
            });
        }
    }
}