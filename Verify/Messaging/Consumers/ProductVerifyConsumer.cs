using MassTransit;
using Messaging.Contracts;
using VERIFY.Model;

namespace VERIFY.Messaging.Consumers;

public sealed class ProductVerifyConsumer(
    IServiceScopeFactory serviceScope,
    ILogger<ProductVerifyConsumer> logger,
    IPublishEndpoint publishEndpoint)
    : IConsumer<ProductVerifyRequested>
{
    public async Task Consume(ConsumeContext<ProductVerifyRequested> context)
    {
        if (context.Message.ProductId <= 0)
        {
            logger.LogWarning("Invalid ProductVerifyRequested message: {ProductId}", context.Message.ProductId);
            return;
        }

        using var scope = serviceScope.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VerifyDbContext>();

        var containProduct = dbContext.VERIFY_PRODUCTS
            .FirstOrDefault(x => x.ProductId == context.Message.ProductId);

        if (containProduct == null) return;

        containProduct.isProductVerified = true;
        containProduct.Description = context.Message.Description;
        containProduct.VerifierId = context.Message.VerifierId;
        containProduct.VerifiedTime = DateTime.UtcNow;

        await dbContext.SaveChangesAsync();

        await publishEndpoint.Publish(new ProductVerified(context.Message.ProductId));

        logger.LogInformation(
            "Product {ProductId} verified successfully by verifier {VerifierId}",
            context.Message.ProductId,
            context.Message.VerifierId);
    }
}