using Microsoft.Extensions.Options;
using PRODUCT.Messaging.Consumers;

namespace PRODUCT.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection("RabbitMq"))
            .ValidateOnStart();

        services.AddSingleton<IRabbitMqConnection>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>();
            return RabbitMqConnection.CreateAsync(options).GetAwaiter().GetResult();
        });

        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

        services.AddHostedService<ProductUnverifiedConsumer>();
        services.AddHostedService<ProductVerifiedConsumer>();

        return services;
    }
}
