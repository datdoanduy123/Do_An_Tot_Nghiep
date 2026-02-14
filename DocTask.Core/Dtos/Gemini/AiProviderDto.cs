using System;
using System.Collections.Generic;

namespace DocTask.Core.Dtos.Gemini
{
    /// <summary>
    /// DTO chung cho mọi AI provider (Ollama, Gemini, v.v.)
    /// Giúp tách biệt logic gọi API khỏi business logic
    /// </summary>
    public class AiProviderDto
    {
        /// <summary>
        /// Message gửi đến AI (role + content)
        /// </summary>
        public class AiMessage
        {
            /// <summary>
            /// Vai trò: "system" hoặc "user"
            /// </summary>
            public string Role { get; set; } = "user";

            /// <summary>
            /// Nội dung tin nhắn
            /// </summary>
            public string Content { get; set; } = "";
        }

        /// <summary>
        /// Request gửi đến AI provider
        /// </summary>
        public class AiCompletionRequest
        {
            /// <summary>
            /// Danh sách messages (system prompt + user message)
            /// </summary>
            public List<AiMessage> Messages { get; set; } = new();

            /// <summary>
            /// Nhiệt độ sinh text: 0.0 = deterministic, 1.0 = creative
            /// </summary>
            public double Temperature { get; set; } = 0.0;

            /// <summary>
            /// Số token tối đa cho response
            /// </summary>
            public int MaxOutputTokens { get; set; } = 8192;

            /// <summary>
            /// Top-P sampling (nucleus sampling)
            /// </summary>
            public double TopP { get; set; } = 0.8;
        }

        /// <summary>
        /// Response trả về từ AI provider
        /// </summary>
        public class AiCompletionResponse
        {
            /// <summary>
            /// Nội dung text response từ AI
            /// </summary>
            public string Text { get; set; } = "";

            /// <summary>
            /// Tên provider đã xử lý request (để log/debug)
            /// </summary>
            public string ProviderUsed { get; set; } = "";

            /// <summary>
            /// Response có thành công không
            /// </summary>
            public bool Success { get; set; } = true;

            /// <summary>
            /// Thông báo lỗi (nếu có)
            /// </summary>
            public string? ErrorMessage { get; set; }
        }

        /// <summary>
        /// Cấu hình tổng hợp cho tất cả AI providers
        /// </summary>
        public class AiProviderOptions
        {
            /// <summary>
            /// API key cho Gemini (optional, dùng khi Ollama lỗi)
            /// </summary>
            public string GeminiApiKey { get; set; } = "";

            /// <summary>
            /// URL gốc của Ollama server (mặc định: http://localhost:11434)
            /// </summary>
            public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

            /// <summary>
            /// Tên model Ollama sử dụng (mặc định: qwen2.5:7b-instruct)
            /// </summary>
            public string OllamaModel { get; set; } = "qwen2.5:7b-instruct";
        }
    }
}
