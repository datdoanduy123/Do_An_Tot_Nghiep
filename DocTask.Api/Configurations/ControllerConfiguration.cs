using DocTask.Core.DTOs.ApiResponses;
using Microsoft.AspNetCore.Mvc;

namespace DockTask.Api.Configurations;

public static class ControllerConfiguration
{
    public static IServiceCollection AddControllerConfiguration(this IServiceCollection services)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            })
            .ConfigureApiBehaviorOptions(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var error = context.ModelState.Values.First().Errors.First().ErrorMessage;
                return new BadRequestObjectResult(new ApiResponse<object>
                {
                    Success = false,
                    Error = error,
                });
            };
        });
        return services;
    }

}