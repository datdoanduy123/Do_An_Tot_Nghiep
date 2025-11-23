using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DocTask.Core.Interfaces.Services
{
    public interface IFileConvertService
    {
        Task<string> GetFileContentAsync(string fileUrl);
        Task<(byte[] fileContent, string contentType)> ConvertFileFormatAsync(
            string fileContent,
            string fileName,
            string format = "pdf"
        );
    }
}