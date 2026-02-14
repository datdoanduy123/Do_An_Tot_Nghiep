using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Data.Repositories;
using DocTask.Service.Services;
using DocTask.Service.Services.Providers;
using Microsoft.AspNetCore.SignalR;
using static DocTask.Core.Dtos.Gemini.AiProviderDto;

namespace DockTask.Api.Configurations
{
    public static class ApplicationContainer
    {
        public static IServiceCollection AddApplicationContainer(this IServiceCollection services)
        {
            // ===== AI Providers (Ollama local → Rule-based fallback) =====
            // ⚠️ Gemini đã bị loại bỏ — chỉ dùng Ollama local (miễn phí, unlimited)
            services.AddHttpClient("OllamaHttpClient");

            // Đăng ký AiProviderFactory — trung tâm điều phối fallback
            services.AddScoped<AiProviderFactory>(sp =>
            {
                var options = sp.GetRequiredService<AiProviderOptions>();
                var httpFactory = sp.GetRequiredService<IHttpClientFactory>();

                // Tạo danh sách providers theo thứ tự ưu tiên:
                // 1. Ollama local (primary) — chạy miễn phí tại localhost
                // 2. RuleBasedFallback — sinh task mặc định nếu Ollama lỗi
                var providers = new List<IAiProvider>
                {
                    new OllamaAiProvider(httpFactory.CreateClient("OllamaHttpClient"), options),
                    new RuleBasedFallbackProvider()
                };

                return new AiProviderFactory(providers);
            });

            // ===== Services =====
            services.AddScoped<ITaskService, TaskService>();
            services.AddScoped<IProgressService, ProgressService>();
            services.AddScoped<IProgressCalculationService, ProgressCalculationService>();
            services.AddScoped<ITaskPermissionService, TaskPermissionService>();
            services.AddScoped<IReportSubmissionService, ReportSubmissionService>();
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<IReminderService, ReminderService>();
            services.AddSingleton<IJwtService, JwtService>();
            services.AddScoped<IUploadFileService, UploadFileService>();
            services.AddScoped<ISubTaskService, SubTaskService>();
            services.AddScoped<IGeminiService, GeminiService>();
            services.AddScoped<IUnitService, UnitService>();
            services.AddTransient<IFileConvertService, FileConvertService>();
            services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
            services.AddScoped<IEmailService, EmailService>();
            services.AddScoped<IAutomationService, AutomationService>();
            services.AddScoped<IEmployeeClusteringService, EmployeeClusteringService>();
            services.AddScoped<IAutoAssignmentService, AutoAssignmentService>();

            // ===== Repositories =====
            services.AddScoped<IEmployeeProfileRepository, EmployeeProfileRepository>();
            services.AddScoped<IEmployeeClusterRepository, EmployeeClusterRepository>();
            services.AddScoped<ITaskRepository, TaskRepository>();
            services.AddScoped<IProgressRepository, ProgressRepository>();
            services.AddScoped<IReminderRepository, ReminderRepository>();
            services.AddScoped<IUploadFileRepository, UploadFileRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<ISubTaskRepository, SubTaskRepository>();
            services.AddScoped<IFrequencyDetailRepository, FrequencyDetailRepository>();
            services.AddScoped<IFrequencyRepository, FrequencyRepository>();
            services.AddScoped<IUnitRepository, UnitRepository>();
            services.AddScoped<IAgentRepository, AgentRepository>();

            services.AddScoped<ITaskDraftRepository, TaskDraftRepository>();
            services.AddScoped<ITaskDraftService, TaskDraftService>();

            return services;
        }
    }
}