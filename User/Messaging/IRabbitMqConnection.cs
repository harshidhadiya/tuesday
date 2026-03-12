using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace USER.Messaging;

public interface IRabbitMqConnection
{
    IConnection Connection { get; }
}

public sealed class RabbitMqConnection : IRabbitMqConnection, IAsyncDisposable
{
    private readonly IConnection _connection;

    public RabbitMqConnection(IOptions<RabbitMqOptions> options)
    {
        var opt = options.Value;
        var factory = new ConnectionFactory
        {
            HostName = opt.HostName,
            Port = opt.Port,
            UserName = opt.UserName,
            Password = opt.Password,
            VirtualHost = opt.VirtualHost
        };
        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
    }

    public IConnection Connection => _connection;
 
   
    public async ValueTask DisposeAsync()
    {
        if (_connection.IsOpen)
        {
            await _connection.CloseAsync();
        }
        await _connection.DisposeAsync();
    }
}
