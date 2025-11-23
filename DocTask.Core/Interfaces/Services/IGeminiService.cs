using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DocTask.Core.Dtos.Gemini;
using static DocTask.Core.Dtos.Gemini.GeminiDto;

namespace DocTask.Core.Interfaces.Services
{
    public interface IGeminiService
    {
        Task<ChatResponse> AskWithFileAsync(int fileId, bool redo);
        Task<ChatResponse?> GetPreviewAsync(int fileId);
        bool RejectPlan(int fileId);
        Task<(byte[] fileContent, string fileName, string contentType)> AskWithTaskSummaryAsync(int taskId, int userId, string format);

        Task<object> AskAsync(
            string userMessage,
            string systemPromptTemplate,
            PromptContextType contextType = PromptContextType.GeneralChat,
            Dictionary<string, string>? additionalContext = null,
            double temperature = 0.0
        );
        Task<object> AskSummaryAsync(string userMessage, PromptContextType contextType, Dictionary<string, string> additionalContext);
        Task<string> AskPlanAsync(
            string userMessage,
            PromptContextType contextType,
            Dictionary<string, string>? additionalContext = null,
            double temperature = 0.0
        );
        //Task<GeminiTaskDto> ApprovePlanAsync(int fileId);
        Task<AgentDto?> CreateAsync(CreateAgentDto createAgentDto);
        Task<AgentDto?> GetByIdAsync(int FileId);
    }
}