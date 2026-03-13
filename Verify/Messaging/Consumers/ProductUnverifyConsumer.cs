using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using VERIFY.Model;

namespace VERIFY.Messaging.Consumers;

public sealed class ProductUnverifyConsumer(
    IServiceScopeFactory scopeFactory,
    ILogger<ProductUnverifyConsumer> logger,
    IPublishEndpoint publishEndpoint)
    : IConsumer<ProductUnverifyRequested>
{
    public async Task Consume(ConsumeContext<ProductUnverifyRequested> context)
    {
        if (context.Message.ProductId <= 0)
        {
            logger.LogWarning("Invalid ProductUnverifyRequested message: {ProductId}", context.Message.ProductId);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VerifyDbContext>();

        var record = await db.VERIFY_PRODUCTS
            .FirstOrDefaultAsync(v => v.ProductId == context.Message.ProductId);

        if (record == null) return;
        if (record.VerifierId != context.Message.AdminId) return;

        record.isProductVerified = false;
        record.Description = context.Message.Description;
        record.VerifiedTime = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await publishEndpoint.Publish(new ProductUnverified(
            ProductId: record.ProductId,
            AdminId: context.Message.AdminId));

        logger.LogInformation("Product {ProductId} unverified by admin {AdminId}", context.Message.ProductId, context.Message.AdminId);
    }
}
