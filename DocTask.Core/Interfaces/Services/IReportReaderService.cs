

namespace DocTask.Core.Interfaces.Services;

public interface IReportReaderService
{
  Task<string> ExtractTextFromIdAsync(int fileId);
}


