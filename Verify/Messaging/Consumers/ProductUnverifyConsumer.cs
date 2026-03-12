using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VERIFY.Messaging.Events;
using VERIFY.Model;


namespace VERIFY.Messaging.Consumers;

public class ProductUnverifyConsumer : BackgroundService
{

    private JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
    private readonly IRabbitMqConnection _rabbitConnection;
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductUnverifyConsumer> _logger;

    private IChannel? _channel;
    IRabbitMqPublisher _publisher;

    public ProductUnverifyConsumer(
        IRabbitMqConnection rabbitConnection,
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<ProductUnverifyConsumer> logger, IRabbitMqPublisher publisher)
    {
        _rabbitConnection = rabbitConnection;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
        this._publisher = publisher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _rabbitConnection.Connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(exchange: "admin.exchange", type: ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: stoppingToken);

        const string queueName = "admin.unverify";
        const string routingKey = "admin.unverify";

        await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queue: queueName, exchange: "admin.exchange", routingKey: routingKey, cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageAsync;

        await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);
    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        if (_channel == null) return;

        try
        {
            var json = Encoding.UTF8.GetString(args.Body.ToArray());
            var message = JsonSerializer.Deserialize<adminUnverifyProductEvent>(json);

            if (message == null || message.productId <= 0)
            {
                _logger.LogWarning("Invalid message on {RoutingKey}", args.RoutingKey);
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VerifyDbContext>();

            var record = await db.VERIFY_PRODUCTS.FirstOrDefaultAsync(v => v.ProductId == message.productId);
            if (record == null)
            {
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                return;
            }
            if (record.VerifierId != message.adminId)
            {
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                return;
            }

            record.isProductVerified = false;
            record.Description = message.description;
            record.VerifiedTime = DateTime.UtcNow;

            await db.SaveChangesAsync();

            await _publisher.PublishAsync("product.unverified", new ProductUnverifiedEvent
            {
                ProductId = record.ProductId,
                AdminId = message.adminId
            });

            _logger.LogInformation("Product {ProductId} unverification processed from admin event", message.productId);
            await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed processing {RoutingKey}", args.RoutingKey);
            await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_channel != null)
            {
                await _channel.CloseAsync(cancellationToken);
                _channel.Dispose();
            }
        }
        catch
        {
        }

        await base.StopAsync(cancellationToken);
    }
}
