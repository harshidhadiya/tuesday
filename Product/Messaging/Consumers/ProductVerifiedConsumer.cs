namespace PRODUCT.Messaging.Consumers;

using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PRODUCT.Messaging.Events;
using PRODUCT.Model;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;


public sealed class ProductVerifiedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRabbitMqConnection _rabbitConnection;
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductVerifiedConsumer> _logger;

    private IChannel? _channel;

    public ProductVerifiedConsumer(
        IRabbitMqConnection rabbitConnection,
        IOptions<RabbitMqOptions> options,
        IServiceScopeFactory scopeFactory,
        ILogger<ProductVerifiedConsumer> logger)
    {
        _rabbitConnection = rabbitConnection;
        _options = options.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _rabbitConnection.Connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(_options.ExchangeName, ExchangeType.Direct, true, false, cancellationToken: stoppingToken);

        const string queueName = "product.verified";
        const string routingKey = "product.verified";

        await _channel.QueueDeclareAsync(queueName, true, false, false, cancellationToken: stoppingToken);
        await _channel.QueueBindAsync(queueName, _options.ExchangeName, routingKey, cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, 10, false, stoppingToken);

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
            var message = JsonSerializer.Deserialize<ProductUnverifiedEvent>(json, JsonOptions);
             Console.WriteLine(message);
            if (message == null || message.productId <= 0)
            {
                _logger.LogWarning("Invalid message on {RoutingKey}", args.RoutingKey);
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MACUTIONDB>();

            var product = await db.PRODUCTS.FirstOrDefaultAsync(p => p.Id == message.productId);
            if (product == null)
            {
                _logger.LogInformation("Product {ProductId} not found to clear auction", message.productId);
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                return;
            }

            product.isVerified=true;

            await db.SaveChangesAsync();

            _logger.LogInformation("Cleared auction for product {ProductId} due to unverification", message.productId);
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
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
