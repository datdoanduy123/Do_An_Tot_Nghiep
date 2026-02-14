using System.Threading.Tasks;
using static DocTask.Core.Dtos.Gemini.AiProviderDto;

namespace DocTask.Core.Interfaces.Services
{
    /// <summary>
    /// Interface chung cho mọi AI provider (Ollama, Gemini, v.v.).
    /// Mỗi provider implement interface này để có thể hoán đổi linh hoạt.
    /// </summary>
    public interface IAiProvider
    {
        /// <summary>
        /// Tên provider (dùng để log/debug), ví dụ: "Ollama", "Gemini"
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Kiểm tra provider có sẵn sàng không (VD: Ollama đang chạy?)
        /// </summary>
        Task<bool> IsAvailableAsync();

        /// <summary>
        /// Gửi request đến AI và nhận response
        /// </summary>
        /// <param name="request">Request chứa messages, temperature, maxTokens</param>
        /// <returns>Response chứa text và metadata</returns>
        Task<AiCompletionResponse> CompleteAsync(AiCompletionRequest request);
    }
}
