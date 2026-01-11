using DocTask.Core.Interfaces.Repositories;
using DocTask.Core.Interfaces.Services;
using DocTask.Data.Repositories;
using DocTask.Service.Services;
using Microsoft.AspNetCore.SignalR;

namespace DockTask.Api.Configurations
{
    public static class ApplicationContainer
    {
        public static IServiceCollection AddApplicationContainer(this IServiceCollection services)
        {
            // Services
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
        
            // Repositories
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
            
            // Other
            services.AddHttpClient<IGeminiService, GeminiService>();
            return services;
        }
    }
}