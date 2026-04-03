using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using VERIFY.Model;

namespace VERIFY.Messaging.Consumers;

public sealed class ProductDeletedConsumer(
    IServiceScopeFactory scopeFactory,
    ILogger<ProductDeletedConsumer> logger)
    : IConsumer<ProductDeleted>
{
    public async Task Consume(ConsumeContext<ProductDeleted> context)
    {
        if (context.Message.ProductId <= 0)
        {
            logger.LogWarning("Invalid ProductDeleted message: {ProductId}", context.Message.ProductId);
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VerifyDbContext>();

        var record = await db.VERIFY_PRODUCTS
            .Where(v => v.ProductId == context.Message.ProductId)
            .FirstOrDefaultAsync();

        if (record == null){
            
            logger.LogError("current productId related record Not Found");
            
             return;}
        if (record.SellerId != context.Message.DeletedByUserId){
            logger.LogError("current productId related seller id is not match with ProductDeletedid");
             return;
             }

        db.VERIFY_PRODUCTS.Remove(record);
        await db.SaveChangesAsync();

        logger.LogInformation("Removed verification record for product {ProductId}", context.Message.ProductId);
    }
}
