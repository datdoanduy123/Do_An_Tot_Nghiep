using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocTask.Core.Interfaces.Services;
using static DocTask.Core.Dtos.Gemini.AiProviderDto;

namespace DocTask.Service.Services.Providers
{
    /// <summary>
    /// Provider gọi AI qua Google Gemini Cloud API.
    /// Dùng làm fallback khi Ollama không khả dụng (nếu có API key).
    /// API endpoint: POST https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent
    /// </summary>
    public class GeminiAiProvider : IAiProvider
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        /// <summary>
        /// Tên provider dùng để log/debug
        /// </summary>
        public string ProviderName => "Gemini";

        /// <summary>
        /// Khởi tạo GeminiAiProvider với HttpClient và API key
        /// </summary>
        public GeminiAiProvider(HttpClient httpClient, AiProviderOptions options)
        {
            _httpClient = httpClient;
            _apiKey = options.GeminiApiKey ?? "";
        }

        /// <summary>
        /// Kiểm tra Gemini có khả dụng không (có API key?)
        /// </summary>
        public Task<bool> IsAvailableAsync()
        {
            // Gemini chỉ khả dụng khi có API key
            return Task.FromResult(!string.IsNullOrWhiteSpace(_apiKey));
        }

        /// <summary>
        /// Gọi Gemini API để sinh text.
        /// Refactor từ GeminiService.AskAsync(), giữ nguyên retry logic.
        /// </summary>
        public async Task<AiCompletionResponse> CompleteAsync(AiCompletionRequest request)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                return new AiCompletionResponse
                {
                    Success = false,
                    ProviderUsed = ProviderName,
                    ErrorMessage = "Gemini API key chưa được cấu hình"
                };
            }

            try
            {
                // Chuyển đổi messages từ format chung sang format Gemini
                // Gemini sử dụng "contents" array với role "user"/"model"
                var contents = request.Messages.Select(m => new
                {
                    role = m.Role == "system" ? "user" : m.Role, // Gemini không có role "system"
                    parts = new object[] { new { text = m.Content } }
                }).ToArray();

                var requestBody = new
                {
                    contents = contents,
                    generationConfig = new
                    {
                        temperature = request.Temperature,
                        candidateCount = 1,
                        topP = request.TopP,
                        topK = 40,
                        maxOutputTokens = request.MaxOutputTokens
                    }
                };

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";

                // Retry logic với exponential backoff (xử lý 429, 503)
                int maxRetries = 3;
                int delayMs = 2000;
                string? rawResponse = null;

                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(60));

                        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
                        {
                            Content = JsonContent.Create(requestBody)
                        };
                        httpRequest.Headers.Add("User-Agent", "DocTaskAI/1.0");

                        var response = await _httpClient.SendAsync(httpRequest, cts.Token);
                        rawResponse = await response.Content.ReadAsStringAsync();

                        // Xử lý rate limit (429)
                        if (response.StatusCode == (HttpStatusCode)429)
                        {
                            Console.WriteLine($"⚠️ [Gemini] Rate Limit 429 (lần {i + 1}/{maxRetries})");
                            await Task.Delay(delayMs);
                            delayMs *= 2;
                            continue;
                        }

                        // Xử lý service unavailable (503)
                        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
                        {
                            Console.WriteLine($"⚠️ [Gemini] Service Unavailable 503 (lần {i + 1}/{maxRetries})");
                            await Task.Delay(delayMs);
                            delayMs *= 2;
                            continue;
                        }

                        response.EnsureSuccessStatusCode();
                        break; // Thành công
                    }
                    catch (HttpRequestException ex)
                    {
                        Console.WriteLine($"❌ [Gemini] Lỗi mạng: {ex.Message}");
                        if (i == maxRetries - 1)
                        {
                            return new AiCompletionResponse
                            {
                                Success = false,
                                ProviderUsed = ProviderName,
                                ErrorMessage = $"Gemini API lỗi sau {maxRetries} lần thử: {ex.Message}"
                            };
                        }
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                    }
                    catch (TaskCanceledException)
                    {
                        Console.WriteLine($"⏱️ [Gemini] Timeout (lần {i + 1}/{maxRetries})");
                        if (i == maxRetries - 1)
                        {
                            return new AiCompletionResponse
                            {
                                Success = false,
                                ProviderUsed = ProviderName,
                                ErrorMessage = "Gemini API timeout"
                            };
                        }
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                    }
                }

                if (string.IsNullOrWhiteSpace(rawResponse))
                {
                    return new AiCompletionResponse
                    {
                        Success = false,
                        ProviderUsed = ProviderName,
                        ErrorMessage = "Không nhận được phản hồi từ Gemini"
                    };
                }

                // Parse response Gemini
                using var doc = JsonDocument.Parse(rawResponse);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString() ?? "";

                Console.WriteLine($"✅ [Gemini] Response nhận được ({text.Length} ký tự)");

                return new AiCompletionResponse
                {
                    Text = text,
                    ProviderUsed = ProviderName,
                    Success = true
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Gemini] Lỗi: {ex.Message}");
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
