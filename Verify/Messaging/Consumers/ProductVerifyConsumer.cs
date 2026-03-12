
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VERIFY.Messaging.Events;
using VERIFY.Model;

namespace VERIFY.Messaging.Consumers;

public class ProductVerifyConsumer : BackgroundService
{
    readonly IRabbitMqConnection connection;
    readonly ILogger<ProductVerifyConsumer> logger;
    readonly IServiceScopeFactory serviceScope;
    IChannel? _channel;
    IRabbitMqPublisher _publisher;

    public ProductVerifyConsumer(IRabbitMqConnection connection, ILogger<ProductVerifyConsumer> logger, IServiceScopeFactory serviceScope, IRabbitMqPublisher _publisher)
    {
        this._publisher = _publisher;
        this.connection = connection;
        this.logger = logger;
        this.serviceScope = serviceScope;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await connection.Connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync(exchange: "admin.exchange", type: ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        const string queueName = "product.verify";
        const string routingKey = "product.verify";
        await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queue: queueName, exchange: "admin.exchange", routingKey: routingKey, cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += verifyProduct;
        await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
    }

    public async Task verifyProduct(object sender, BasicDeliverEventArgs args)
    {
        if (_channel == null) return;

        using var scope = serviceScope.CreateScope();
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VerifyDbContext>();
            var body = args.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            Console.WriteLine(json);
            var product = JsonSerializer.Deserialize<ProductVerifyEvent>(json);
            if (product != null)
            {
                var containProduct = dbContext.VERIFY_PRODUCTS.Where(x => x.ProductId == product.productId).FirstOrDefault();
                if (containProduct == null)
                {
                    await _channel.BasicAckAsync(args.DeliveryTag, false);
                    return;
                }

                containProduct.isProductVerified = true;
                containProduct.Description = product.description;
                containProduct.VerifierId = product.verifierId;
                containProduct.VerifiedTime = DateTime.Now;

                await dbContext.SaveChangesAsync();
                await _publisher.PublishAsync("product.verified", new
                {
                    product.productId
                });
                logger.LogInformation("Product {ProductId} verified successfully by verifier {VerifierId}", product.productId, product.verifierId);
            }
            await _channel.BasicAckAsync(args.DeliveryTag, false);
        }
        catch (System.Exception)
        {
            logger.LogError("Error processing message with DeliveryTag: {DeliveryTag}", args.DeliveryTag);
            await _channel.BasicNackAsync(args.DeliveryTag, false, true);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }
        await base.StopAsync(cancellationToken);
    }
}