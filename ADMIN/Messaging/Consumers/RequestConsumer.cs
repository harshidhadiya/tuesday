
using System.Text;
using System.Text.Json;
using ADMIN.Messaging.Events;
using ADMIN.Model;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ADMIN.Messaging.Consumers;
// this basically is used for the establish the create the instance of the verified admin okay 


public class RequestConsumer : BackgroundService
{
    ILogger<RequestConsumer> _logger;
    IRabbitMqConnection _connection;
    IChannel ?_channel;
    private readonly IServiceScopeFactory scope;
    public RequestConsumer(ILogger<RequestConsumer> logger, IRabbitMqConnection connection, IServiceScopeFactory scope)
    {
        _logger = logger;
        _connection = connection;
        this.scope = scope;


    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = await _connection.Connection.CreateChannelAsync();
       await  _channel.ExchangeDeclareAsync(exchange: "admin", type: ExchangeType.Direct, durable: true, autoDelete: false);
       await  _channel.QueueDeclareAsync(queue: "admin.request", durable: true, exclusive: false, autoDelete: false);
       await  _channel.QueueBindAsync(queue: "admin.request", exchange: "admin", routingKey: "request.created");
       await  _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false);
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += CreateRequestAsync;
        await _channel.BasicConsumeAsync(queue: "admin.request", autoAck: false, consumer: consumer);
        
    }

   public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_channel!= null && _channel.IsOpen )
        {
            await _channel.CloseAsync(cancellationToken);
        }
        if(_channel!=null)
        await _channel.DisposeAsync();
        base.Dispose();
    }


    public async Task CreateRequestAsync(object sender, BasicDeliverEventArgs args)
{
    if (_channel == null)
    {
        return ;
    }
    try
    {
        var json = Encoding.UTF8.GetString(args.Body.ToArray());
        _logger.LogInformation("Received request.created event with body: {Json}", json);

        var correctdata = JsonSerializer.Deserialize<CreateRequest>(json);

        if (correctdata == null)
        {
            await _channel.BasicNackAsync(args.DeliveryTag, false, false);
            return;
        }
        Console.WriteLine(correctdata.requestUserId);
        using var scoped = scope.CreateScope();
        var db = scoped.ServiceProvider.GetRequiredService<MACUTIONDB>();

        var createRequest = new RequestTable
        {
            RequestUserId = correctdata.requestUserId,
            VerifiedByAdmin = false,
            VerifierId = 0,
            CreatedAt = DateTime.UtcNow,
            Name=correctdata.name,
            Email=correctdata.email
        };

        var exist_user= await db.REQUESTS.Where(x=>x.RequestUserId==correctdata.requestUserId).FirstOrDefaultAsync();
        if(exist_user!=null)
        {
            _logger.LogWarning("Request already exists for user id: {UserId}", correctdata.requestUserId);
            await _channel.BasicAckAsync(args.DeliveryTag, false);
            return;
        }

        await db.REQUESTS.AddAsync(createRequest);
        await db.SaveChangesAsync();

        await _channel.BasicAckAsync(args.DeliveryTag, false);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error processing request.created event");
        await _channel.BasicNackAsync(args.DeliveryTag, false, true);
    }
}
}