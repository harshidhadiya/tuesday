using Microsoft.AspNetCore.Diagnostics;

namespace AUCTION.ExceptionHandler
{
    public class GlobalHandler : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine("entered here several time");
            var exceptionHandlerFeature = httpContext.Features.Get<IExceptionHandlerFeature>();
            if (exceptionHandlerFeature != null)
            {
                var ex = exceptionHandlerFeature.Error;
                httpContext.Response.StatusCode = 500;
                httpContext.Response.ContentType = "application/json";
                var response = new { message = ex.Message };
                httpContext.Response.WriteAsJsonAsync(response, cancellationToken);
                return new ValueTask<bool>(true);
            }
            return new ValueTask<bool>(false);
        }
    }
}