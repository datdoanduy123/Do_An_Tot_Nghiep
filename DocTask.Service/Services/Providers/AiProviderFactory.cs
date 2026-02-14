using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using static DocTask.Core.Dtos.Gemini.AiProviderDto;
using DocTask.Core.Interfaces.Services;

namespace DocTask.Service.Services.Providers
{
    /// <summary>
    /// Factory điều phối AI providers với fallback tự động.
    /// Thứ tự ưu tiên: Ollama (local) → Gemini (cloud) → Rule-based (template).
    /// Đảm bảo hệ thống LUÔN trả về kết quả, không bao giờ die.
    /// </summary>
    public class AiProviderFactory
    {
        private readonly List<IAiProvider> _providers;

        /// <summary>
        /// Khởi tạo factory với danh sách providers theo thứ tự ưu tiên.
        /// Provider đầu tiên được thử trước, nếu lỗi thì thử provider tiếp theo.
        /// </summary>
        /// <param name="providers">
        /// Danh sách providers theo thứ tự ưu tiên giảm dần.
        /// Thường là: [OllamaAiProvider, GeminiAiProvider, RuleBasedFallbackProvider]
        /// </param>
        public AiProviderFactory(IEnumerable<IAiProvider> providers)
        {
            _providers = new List<IAiProvider>(providers);

            // Log danh sách providers đã đăng ký
            Console.WriteLine($"🏭 [AiProviderFactory] Đã đăng ký {_providers.Count} providers:");
            foreach (var provider in _providers)
            {
                Console.WriteLine($"   → {provider.ProviderName}");
            }
        }

        /// <summary>
        /// Gọi AI với fallback tự động.
        /// Thử từng provider theo thứ tự, nếu provider hiện tại lỗi thì chuyển sang provider tiếp theo.
        /// </summary>
        /// <param name="request">Request chuẩn (messages, temperature, maxTokens)</param>
        /// <returns>Response từ provider thành công đầu tiên</returns>
        /// <exception cref="Exception">Ném ra khi TẤT CẢ providers đều lỗi</exception>
        public async Task<AiCompletionResponse> CompleteAsync(AiCompletionRequest request)
        {
            var errors = new List<string>();

            foreach (var provider in _providers)
            {
                try
                {
                    // Bước 1: Kiểm tra provider có sẵn sàng không
                    var isAvailable = await provider.IsAvailableAsync();
                    if (!isAvailable)
                    {
                        var skipMsg = $"[{provider.ProviderName}] Không khả dụng, bỏ qua";
                        Console.WriteLine($"⏭️ {skipMsg}");
                        errors.Add(skipMsg);
                        continue;
                    }

                    // Bước 2: Gọi provider
                    Console.WriteLine($"🔄 [AiProviderFactory] Đang thử {provider.ProviderName}...");
                    var response = await provider.CompleteAsync(request);

                    // Bước 3: Kiểm tra kết quả
                    if (response.Success && !string.IsNullOrWhiteSpace(response.Text))
                    {
                        Console.WriteLine($"✅ [AiProviderFactory] Thành công với {provider.ProviderName}");
                        return response;
                    }

                    // Provider trả về nhưng không thành công → thử provider tiếp
                    var failMsg = $"[{provider.ProviderName}] Thất bại: {response.ErrorMessage ?? "Response trống"}";
                    Console.WriteLine($"⚠️ {failMsg}");
                    errors.Add(failMsg);
                }
                catch (Exception ex)
                {
                    // Provider throw exception → thử provider tiếp
                    var errorMsg = $"[{provider.ProviderName}] Exception: {ex.Message}";
                    Console.WriteLine($"❌ {errorMsg}");
                    errors.Add(errorMsg);
                }
            }

            // Tất cả providers đều lỗi (không nên xảy ra vì RuleBased luôn thành công)
            var allErrors = string.Join("\n", errors);
            throw new Exception($"Tất cả AI providers đều thất bại:\n{allErrors}");
        }

        /// <summary>
        /// Lấy tên provider đang khả dụng đầu tiên (dùng để hiển thị UI)
        /// </summary>
        public async Task<string> GetActiveProviderNameAsync()
        {
            foreach (var provider in _providers)
            {
                if (await provider.IsAvailableAsync())
                {
                    return provider.ProviderName;
                }
            }
            return "None";
        }
    }
}
