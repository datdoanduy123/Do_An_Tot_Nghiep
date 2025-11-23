using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Gemini;

namespace DocTask.Core.Interfaces.Services
{
    public interface IAutomationService
    {
        Task<TaskAutomationDto> TaskAnalyzeAsync(AgentRequestDto request, bool redo = false);
        Task<List<TaskExecutionResultDto>> ExecuteActionAsync(TaskAutomationDto tasks, int userId);
    }
}