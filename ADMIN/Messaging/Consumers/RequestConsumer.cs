using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using ADMIN.Model;

namespace ADMIN.Messaging.Consumers;

public sealed class RequestConsumer(
    ILogger<RequestConsumer> logger,
    IServiceScopeFactory scopeFactory)
    : IConsumer<AdminRegistrationRequested>
{
    public async Task Consume(ConsumeContext<AdminRegistrationRequested> context)
    {
        if (context.Message.RequestUserId <= 0)
        {
            logger.LogWarning("Invalid AdminRegistrationRequested message: {UserId}", context.Message.RequestUserId);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();

        var existUser = await db.REQUESTS
            .Where(x => x.RequestUserId == context.Message.RequestUserId)
            .FirstOrDefaultAsync();

        if (existUser != null)
        {
            logger.LogWarning("Request already exists for user id: {UserId}", context.Message.RequestUserId);
            return;
        }

        var createRequest = new RequestTable
        {
            RequestUserId = context.Message.RequestUserId,
            VerifiedByAdmin = false,
            VerifierId = 0,
            CreatedAt = DateTime.UtcNow,
            Name = context.Message.Name,
            Email = context.Message.Email
        };

        await db.REQUESTS.AddAsync(createRequest);
        await db.SaveChangesAsync();
    }
}