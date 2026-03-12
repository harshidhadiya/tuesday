using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using VERIFY.Messaging.Events;
using VERIFY.Model;

namespace VERIFY.Messaging.Consumers;

public sealed class ProductDeletedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRabbitMqConnection _rabbitConnection;
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductDeletedConsumer> _logger;

    private IChannel? _channel;

    public ProductDeletedConsumer(
        IRabbitMqConnection rabbitConnection,
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<ProductDeletedConsumer> logger)
    {
        _rabbitConnection = rabbitConnection;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _rabbitConnection.Connection.CreateChannelAsync(cancellationToken: stoppingToken);
        Console.WriteLine($"Declaring exchange {_options.ExchangeName} and queue product.deleted");
        await _channel.ExchangeDeclareAsync(exchange: _options.ExchangeName, type: ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: stoppingToken);

        const string queueName = "product.deleted";
        const string routingKey = "product.deleted";

        await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queue: queueName, exchange: _options.ExchangeName, routingKey: routingKey, cancellationToken: stoppingToken);

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
            var message = JsonSerializer.Deserialize<ProductDeletedEvent>(json, JsonOptions);

            if (message == null || message.productId <= 0)
            {
                _logger.LogWarning("Invalid message on {RoutingKey}", args.RoutingKey);
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                return;
            }
            Console.WriteLine(json + "listening ++ ++ ++ ++ ++ ++ +");

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VerifyDbContext>();

            var record = await db.VERIFY_PRODUCTS.Where(v => v.ProductId == message.productId).FirstOrDefaultAsync();
            if (record == null)
            {
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                return;
            }
            if (record.SellerId != message.deletedByUserId)
            {
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                return;
            }

            db.VERIFY_PRODUCTS.Remove(record);
            await db.SaveChangesAsync();

            _logger.LogInformation("Removed verification for deleted product {ProductId}", message.productId);
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
