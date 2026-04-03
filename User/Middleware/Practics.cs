using Microsoft.AspNetCore.Mvc.Filters;

public class data : IActionFilter
{
    public void OnActionExecuted(ActionExecutedContext context)
    {
        return ;
    }

    public void OnActionExecuting(ActionExecutingContext context)
    {
        return ;
    }
}