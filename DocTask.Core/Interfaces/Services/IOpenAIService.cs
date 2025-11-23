using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Dtos.OpenAIDto;

namespace DocTask.Core.Interfaces.Services
{
    public interface IOpenAIService
    {
        Task<OpenAIDto.ResponseDto> AskAsync(OpenAIDto.RequestDto request);
        Task<OpenAIDto.ResponseDto> AskWithFileAsync(OpenAIDto.RequestDto request, int fileId);
        Task<(byte[] fileContent,string fileName, string contentType)> DownloadSummaryReportAsync(
            int taskId,
            int userId,
            string format,
            DateTime? from = null,
            DateTime? to = null,
            string? status = null,
            int? assigneeId = null
        );
        Task<OpenAIDto.ListActionDto> AnalyzeAutomationAsync(OpenAIDto.AgentRequestDto request);
        Task<OpenAIDto.ActionExecutionResultDto> ExecuteActionAsync(OpenAIDto.ActionDto action, int userId);
        Task<OpenAIDto.AgentExecutionResultDto> AutomateTaskWorkflowAsync(OpenAIDto.AgentRequestDto request, int userId);
    }
}