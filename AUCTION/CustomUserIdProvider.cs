using Microsoft.AspNetCore.SignalR;

public class CustomUserIdProvider(IServiceScopeFactory factory) : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var scope=factory.CreateScope();
        var logger= scope.ServiceProvider.GetRequiredService<ILogger<CustomUserIdProvider>>();
        logger.LogInformation(connection.User?.FindFirst("ID")?.Value+"this is the id of the u sending to the user correct right ");
        return connection.User?.FindFirst("ID")?.Value;
    }
}