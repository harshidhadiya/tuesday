using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using PRODUCT.Model;

namespace PRODUCT.Messaging.Consumers;

public sealed class ProductUnverifiedConsumer(
    IServiceScopeFactory scopeFactory,
    ILogger<ProductUnverifiedConsumer> logger)
    : IConsumer<ProductUnverified>
{
    public async Task Consume(ConsumeContext<ProductUnverified> context)
    {
        if (context.Message.ProductId <= 0)
        {
            logger.LogWarning("Invalid ProductUnverified message: {ProductId}", context.Message.ProductId);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();

        var product = await db.PRODUCTS.FirstOrDefaultAsync(p => p.Id == context.Message.ProductId);
        if (product == null)
        {
            logger.LogInformation("Product {ProductId} not found to unverify", context.Message.ProductId);
            return;
        }

        product.AuctionStartTime = null;
        product.AuctionEndTime = null;
        product.isVerified = false;

        await db.SaveChangesAsync();

        logger.LogInformation("Product {ProductId} marked unverified (admin {AdminId})", context.Message.ProductId, context.Message.AdminId);
    }
}
