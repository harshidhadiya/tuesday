using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace PRODUCT.Messaging;

public interface IRabbitMqConnection
{
    IConnection Connection { get; }
}

public sealed class RabbitMqConnection : IRabbitMqConnection, IAsyncDisposable
{
    private IConnection _connection = null!;

    public IConnection Connection => _connection;

    public static async Task<RabbitMqConnection> CreateAsync(IOptions<RabbitMqOptions> options)
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

        var instance = new RabbitMqConnection();
        instance._connection = await factory.CreateConnectionAsync();
        return instance;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsOpen)
            await _connection.CloseAsync();

        _connection.Dispose();
    }
}
