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

        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

        services.AddHostedService<ProductUnverifiedConsumer>();

        return services;
    }
}

