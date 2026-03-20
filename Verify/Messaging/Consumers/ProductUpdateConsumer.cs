using MassTransit;
using Messaging.Contracts;
using Microsoft.EntityFrameworkCore;
using VERIFY.Model;

namespace VERIFY.Messaging.Consumers
{
    public class ProductUpdateConsumer(VerifyDbContext db) : IConsumer<ProductUpdateForVerification>
    {
        public async Task Consume(ConsumeContext<ProductUpdateForVerification> context)
        {
            var data=await db.VERIFY_PRODUCTS.Where(X=>X.ProductId==context.Message.ProductId).FirstOrDefaultAsync();
            if(data is null)
            return ;
            if(context.Message.descripiton!=null)
            data.Product_description=context.Message.descripiton;
            if(context.Message.name!=null)
            data.ProductName=context.Message.name;

            await db.SaveChangesAsync();
            
        }
    }
}