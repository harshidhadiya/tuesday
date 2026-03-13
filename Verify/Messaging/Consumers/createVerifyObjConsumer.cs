using MassTransit;
using Messaging.Contracts;
using VERIFY.Model;

namespace VERIFY.Messaging.Consumers;

public sealed class createVerifyObjConsumer(
    IServiceScopeFactory serviceScope,
    ILogger<createVerifyObjConsumer> logger)
    : IConsumer<ProductCreatedForVerification>
{
    public async Task Consume(ConsumeContext<ProductCreatedForVerification> context)
    {
        if (context.Message.ProductId <= 0)
        {
            logger.LogWarning("Invalid ProductCreatedForVerification message: {ProductId}", context.Message.ProductId);
            return;
        }

        using var scope = serviceScope.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VerifyDbContext>();

        var containProduct = dbContext.VERIFY_PRODUCTS
            .FirstOrDefault(x => x.ProductId == context.Message.ProductId);

        if (containProduct != null) return;

        var addVerify = new VerifyProductTable
        {
            ProductId = context.Message.ProductId,
            SellerId = context.Message.SellerId,
            ProductName = context.Message.ProductName,
            isProductVerified = false,
            Description = "Pending admin verification."
        };

        await dbContext.VERIFY_PRODUCTS.AddAsync(addVerify);
        await dbContext.SaveChangesAsync();
    }
}