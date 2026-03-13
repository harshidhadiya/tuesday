using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using PRODUCT.Model;

namespace PRODUCT.Messaging.Consumers;

public sealed class ProductVerifiedConsumer(
    IServiceScopeFactory scopeFactory,
    ILogger<ProductVerifiedConsumer> logger)
    : IConsumer<ProductVerified>
{
    public async Task Consume(ConsumeContext<ProductVerified> context)
    {
        if (context.Message.ProductId <= 0)
        {
            logger.LogWarning("Invalid ProductVerified message: {ProductId}", context.Message.ProductId);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();

        var product = await db.PRODUCTS.FirstOrDefaultAsync(p => p.Id == context.Message.ProductId);
        if (product == null)
        {
            logger.LogInformation("Product {ProductId} not found to verify", context.Message.ProductId);
            return;
        }

        product.isVerified = true;
        await db.SaveChangesAsync();

        logger.LogInformation("Product {ProductId} marked verified", context.Message.ProductId);
    }
}
