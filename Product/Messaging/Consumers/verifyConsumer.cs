using MassTransit;
using PRODUCT.Messaging.Events;

namespace PRODUCT.Messaging.Consumers
{
    public class verifyConsumer(ILogger<verifyConsumer> logger) : IConsumer<verifyEvent>
    {
        public Task Consume(ConsumeContext<verifyEvent> context)
        {
            logger.LogWarning(context.Message.ProductId.ToString());
            return Task.CompletedTask;
        }
    }
}