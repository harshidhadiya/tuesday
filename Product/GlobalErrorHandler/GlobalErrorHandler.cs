using Microsoft.AspNetCore.Diagnostics;
using PRODUCT.Data.Dto;

namespace PRODUCT.GlobalErrorHandler
{
    public class GlobalErrorHandler(
        ILogger<GlobalErrorHandler> logger,
        IHostEnvironment env) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            int statusCode = exception switch
            {
                ArgumentException => StatusCodes.Status400BadRequest,
                InvalidOperationException => StatusCodes.Status400BadRequest,
                UnauthorizedAccessException => StatusCodes.Status403Forbidden,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError
            };

            httpContext.Response.StatusCode = statusCode;

            string message;

            if (env.IsDevelopment())
            {
                message = exception.Message;
            }
            else
            {
                message = "An unexpected error occurred. Please try again later.";
            }

            logger.LogError(exception,
                "Unhandled exception occurred. TraceId: {TraceId}",
                httpContext.TraceIdentifier);

            await httpContext.Response.WriteAsJsonAsync(
                ApiResponse<string>.ErrorResponse(message, statusCode),
                cancellationToken
            );

            return true;
        }
    }
}