using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using USER.Messaging.Events;

namespace USER.Messaging.Consumers;

public sealed class RequestCreatedConsumer : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRabbitMqConnection _rabbitConnection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RequestCreatedConsumer> _logger;
    private IChannel? _channel;

    public RequestCreatedConsumer(
        IRabbitMqConnection rabbitConnection,
        IOptions<RabbitMqOptions> options,
        ILogger<RequestCreatedConsumer> logger)
    {
        _rabbitConnection = rabbitConnection;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _rabbitConnection.Connection.CreateChannelAsync();
        await _channel.ExchangeDeclareAsync(exchange: _options.ExchangeName, type: ExchangeType.Direct, durable: true, autoDelete: false);

        const string queueName = "user.request-created";
        const string routingKey = "request.created";

        await _channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false);
        await _channel.QueueBindAsync(queue: queueName, exchange: _options.ExchangeName, routingKey: routingKey);
        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += OnMessageAsync;
        await _channel.BasicConsumeAsync(queue: queueName, autoAck: false, consumer: consumer);

        _logger.LogInformation("RequestCreatedConsumer started, listening for {RoutingKey}", routingKey);

    }

    private async Task OnMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        if (_channel == null) return;

        try
        {
            var json = Encoding.UTF8.GetString(args.Body.ToArray());
            var message = JsonSerializer.Deserialize<RequestCreatedEvent>(json, JsonOptions);

            if (message == null || message.RequestId <= 0)
            {
                _logger.LogWarning("Invalid request.created message received");
              await  _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                return;
            }

            _logger.LogInformation(
                "Request created: RequestId={RequestId}, UserId={UserId} (consumed by User service)",
                message.RequestId,
                message.RequestUserId);

           await _channel.BasicAckAsync(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process request.created");
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
                _channel?.Dispose();
            }
        }
        catch { /* ignore */ }
       await base.StopAsync(cancellationToken);
    }
}
