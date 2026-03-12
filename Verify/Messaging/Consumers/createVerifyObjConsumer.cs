
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VERIFY.Messaging.Events;
using VERIFY.Model;

namespace VERIFY.Messaging.Consumers;

public class createVerifyObjConsumer : BackgroundService
{
    readonly IRabbitMqConnection connection;
    readonly ILogger<createVerifyObjConsumer> logger;
    readonly IServiceScopeFactory serviceScope;
    IChannel? _channel;

    public createVerifyObjConsumer(IRabbitMqConnection connection, ILogger<createVerifyObjConsumer> logger, IServiceScopeFactory serviceScope)
    {
        this.connection = connection;
        this.logger = logger;
        this.serviceScope = serviceScope;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await connection.Connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await _channel.ExchangeDeclareAsync(exchange: "product", type: ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        const string queueName = "product.create";
        const string routingKey = "product.create";
        await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queue: queueName, exchange: "product", routingKey: routingKey, cancellationToken: stoppingToken);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += createVerifyObj;
        await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
    }

    public async Task createVerifyObj(object sender, BasicDeliverEventArgs args)
    {
        if (_channel == null) return;

        using var scope = serviceScope.CreateScope();
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VerifyDbContext>();
            var body = args.Body.ToArray();
            var json = Encoding.UTF8.GetString(body);
            Console.WriteLine(json);
            var product = JsonSerializer.Deserialize<ProductCreateVerifyEvent>(json);
            if (product != null)
            {
                var containProduct = dbContext.VERIFY_PRODUCTS.Where(x => x.ProductId == product.productId).FirstOrDefault();
                if (containProduct == null)
                {
                    var addVerify = new VerifyProductTable
                    {
                        ProductId = product.productId,
                        SellerId = product.sellerId,
                        ProductName = product.productName,
                        isProductVerified = false,
                        Description = "Pending admin verification."
                    };
                    await dbContext.VERIFY_PRODUCTS.AddAsync(addVerify);

                    await dbContext.SaveChangesAsync();
                }
            }
            await _channel.BasicAckAsync(args.DeliveryTag, false);
        }
        catch (System.Exception)
        {
            logger.LogError("Here This Service Not Able to Create Verify Object Error processing message with DeliveryTag: {DeliveryTag}", args.DeliveryTag);
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