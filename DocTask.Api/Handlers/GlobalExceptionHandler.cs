using DocTask.Core.DTOs.ApiResponses;
using DocTask.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace DockTask.Api.Handlers;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }
    
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "AN ERROR OCCURRED: {Message}", exception.Message);
        httpContext.Response.ContentType = "application/json";
        
        int statusCode = exception switch
        {
            BaseException be => be.StatusCode,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status409Conflict,
            DbUpdateException => StatusCodes.Status409Conflict,
            IOException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        httpContext.Response.StatusCode = statusCode;
        
        var result = new ApiResponse<object>
        {
            Success = false,
            Message = null,
            Error = exception.Message,
        };
        await httpContext.Response.WriteAsJsonAsync(result, cancellationToken);
        return true;
    }
}