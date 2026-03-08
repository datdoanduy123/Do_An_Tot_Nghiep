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
                var options     = sp.GetRequiredService<AiProviderOptions>();
                var httpFactory = sp.GetRequiredService<IHttpClientFactory>();

                // Load rules AI từ DB — RuleBasedFallbackProvider không còn hardcode
                var aiRuleRepo  = sp.GetRequiredService<IAiTaskRuleRepository>();
                var allRules    = aiRuleRepo.GetAllAsync().GetAwaiter().GetResult();

                // Providers theo thứ tự ưu tiên:
                // 1. Ollama local (primary) — chạy miễn phí tại localhost
                // 2. RuleBasedFallback — đọc rule từ DB, sinh task khi Ollama lỗi
                var providers = new List<IAiProvider>
                {
                    new OllamaAiProvider(httpFactory.CreateClient("OllamaHttpClient"), options),
                    new RuleBasedFallbackProvider(allRules)
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

            // Service quản lý rule cấu hình AI sinh task
            services.AddScoped<IAiTaskRuleService, AiTaskRuleService>();

            // ===== Pipeline AI Project Generation (Upload → Parse → Plan → Rule → Insert → Assign) =====
            // DocumentTemplateParser: bóc tách tài liệu theo template (deterministic, không AI)
            services.AddScoped<DocTask.Api.Services.DocumentTemplateParser>();
            // AgilePlanner: convert sang cây Agile 4 tầng (Ollama + validate JSON + fallback)
            services.AddScoped<DocTask.Api.Services.AgilePlanner>();
            // AiRuleApplier: áp rule từ DB lên AgilePlanDto (SkillKeyword / ModuleKeyword / DefaultPhase)
            services.AddScoped<DocTask.Api.Services.AiRuleApplier>();
            // AiProjectGenerationService: orchestrator chính — điều phối toàn bộ 8 bước
            services.AddScoped<IAiProjectGenerationService, DocTask.Api.Services.AiProjectGenerationService>();

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

            // Repository cho AI Rule — dị vụ cấu hình rule sinh task
            services.AddScoped<IAiTaskRuleRepository, AiTaskRuleRepository>();

            return services;
        }
    }
}