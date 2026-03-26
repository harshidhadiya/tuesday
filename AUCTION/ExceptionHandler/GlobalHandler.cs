using Microsoft.AspNetCore.Diagnostics;

namespace AUCTION.ExceptionHandler
{
    public class GlobalHandler : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine("entered here several time");
            Console.WriteLine(exception.Message);
            Console.WriteLine(exception.StackTrace);
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
















//  Object reference not set to an instance of an object.                                                                                                                                                                                                                                  
// auction-service        |    at AUCTION.Services.WatchlistService.GetWatchedAuctionsAsync(Int32 userId, WatchListFilterRequest filter) in /src/AUCTION/Services/WatchlistService.cs:line 72
// auction-service        |    at AUCTION.Controllers.WatchlistController.GetWatched(WatchListFilterRequest filter) in /src/AUCTION/Controllers/WatchlistController.cs:line 45                                                                                                                                     
// auction-service        |    at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.TaskOfIActionResultExecutor.Execute(ActionContext actionContext, IActionResultTypeMapper mapper, ObjectMethodExecutor executor, Object controller, Object[] arguments)
// auction-service        |    at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeActionMethodAsync>g__Awaited|12_0(ControllerActionInvoker invoker, ValueTask`1 actionResultValueTask)                                                                                                     
// auction-service        |    at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeNextActionFilterAsync>g__Awaited|10_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)                                                         
// auction-service        |    at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Rethrow(ActionExecutedContextSealed context)                                                                                                                                                                     
// auction-service        |    at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.Next(State& next, Scope& scope, Object& state, Boolean& isCompleted)                                                                                                                                             
// auction-service        |    at Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker.<InvokeInnerFilterAsync>g__Awaited|13_0(ControllerActionInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)
// auction-service        |    at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeFilterPipelineAsync>g__Awaited|20_0(ResourceInvoker invoker, Task lastTask, State next, Scope scope, Object state, Boolean isCompleted)                                                                           
// auction-service        |    at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)                                                                                                                                      
// auction-service        |    at Microsoft.AspNetCore.Mvc.Infrastructure.ResourceInvoker.<InvokeAsync>g__Awaited|17_0(ResourceInvoker invoker, Task task, IDisposable scope)                                                                                                                                      
// auction-service        |    at Microsoft.AspNetCore.Authorization.AuthorizationMiddleware.Invoke(HttpContext context)
// auction-service        |    at Microsoft.AspNetCore.Authentication.AuthenticationMiddleware.Invoke(HttpContext context)                                                                                                                                                                                         
// auction-service        |    at Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddlewareImpl.<Invoke>g__Awaited|10_0(ExceptionHandlerMiddlewareImpl middleware, HttpContext context, Task task)                  