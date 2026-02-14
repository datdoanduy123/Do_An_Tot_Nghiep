using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using static DocTask.Core.Dtos.Gemini.AiProviderDto;
using DocTask.Core.Interfaces.Services;

namespace DocTask.Service.Services.Providers
{
    /// <summary>
    /// Provider gọi AI qua Ollama local server.
    /// Sử dụng model qwen2.5:7b-instruct chạy trên máy local.
    /// API endpoint: POST http://localhost:11434/api/chat
    /// </summary>
    public class OllamaAiProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _model;

        /// <summary>
        /// Tên provider dùng để log/debug
        /// </summary>
        public string ProviderName => "Ollama";

        /// <summary>
        /// Khởi tạo OllamaAiProvider với HttpClient và cấu hình
        /// </summary>
        /// <param name="httpClient">HttpClient cho HTTP calls</param>
        /// <param name="options">Cấu hình Ollama (URL, model name)</param>
        public OllamaAiProvider(HttpClient httpClient, AiProviderOptions options)
        {
            _httpClient = httpClient;
            _baseUrl = options.OllamaBaseUrl?.TrimEnd('/') ?? "http://localhost:11434";
            _model = options.OllamaModel ?? "qwen2.5:7b-instruct";
        }

        /// <summary>
        /// Kiểm tra Ollama server có đang chạy không
        /// Gọi GET /api/tags để verify (timeout 5 giây)
        /// </summary>
        public async Task<bool> IsAvailableAsync()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                Console.WriteLine($"🔍 [Ollama] Kiểm tra kết nối tới {_baseUrl}/api/tags ...");
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", cts.Token);
                Console.WriteLine($"🔍 [Ollama] Status: {response.StatusCode}");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                // Ollama không chạy hoặc không kết nối được
                Console.WriteLine($"❌ [Ollama] IsAvailable lỗi: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gọi Ollama API để sinh text.
        /// Format request theo Ollama Chat API: POST /api/chat
        /// </summary>
        public async Task<AiCompletionResponse> CompleteAsync(AiCompletionRequest request)
        {
            try
            {
                // Xây dựng messages array cho Ollama API
                var messages = new object[request.Messages.Count];
                for (int i = 0; i < request.Messages.Count; i++)
                {
                    messages[i] = new
                    {
                        role = request.Messages[i].Role,
                        content = request.Messages[i].Content
                    };
                }

                // Request body cho Ollama /api/chat endpoint
                var requestBody = new
                {
                    model = _model,
                    messages = messages,
                    stream = false, // Không dùng streaming, chờ response đầy đủ
                    options = new
                    {
                        temperature = request.Temperature,
                        top_p = request.TopP,
                        num_predict = request.MaxOutputTokens
                    }
                };

                // Timeout dài hơn cho local model (có thể chậm trên CPU)
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(120));

                var url = $"{_baseUrl}/api/chat";
                Console.WriteLine($"🤖 [Ollama] Gọi model {_model} tại {url}");

                var httpResponse = await _httpClient.PostAsJsonAsync(url, requestBody, cts.Token);
                var rawResponse = await httpResponse.Content.ReadAsStringAsync();

                if (!httpResponse.IsSuccessStatusCode)
                {
                    Console.WriteLine($"❌ [Ollama] HTTP {httpResponse.StatusCode}: {rawResponse}");
                    return new AiCompletionResponse
                    {
                        Success = false,
                        ProviderUsed = ProviderName,
                        ErrorMessage = $"Ollama trả về HTTP {httpResponse.StatusCode}"
                    };
                }

                // Parse response từ Ollama
                // Format: { "message": { "role": "assistant", "content": "..." }, ... }
                using var doc = JsonDocument.Parse(rawResponse);
                var text = doc.RootElement
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";

                Console.WriteLine($"✅ [Ollama] Response nhận được ({text.Length} ký tự)");

                return new AiCompletionResponse
                {
                    Text = text,
                    ProviderUsed = ProviderName,
                    Success = true
                };
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("⏱️ [Ollama] Request timeout (120s)");
                return new AiCompletionResponse
                {
                    Success = false,
                    ProviderUsed = ProviderName,
                    ErrorMessage = "Ollama request timeout sau 120 giây"
                };
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"❌ [Ollama] Lỗi kết nối: {ex.Message}");
                return new AiCompletionResponse
                {
                    Success = false,
                    ProviderUsed = ProviderName,
                    ErrorMessage = $"Không thể kết nối Ollama: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Ollama] Lỗi không xác định: {ex.Message}");
                return new AiCompletionResponse
                {
                    Success = false,
                    ProviderUsed = ProviderName,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}
