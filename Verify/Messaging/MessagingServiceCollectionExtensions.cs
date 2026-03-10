using VERIFY.Messaging.Consumers;

namespace VERIFY.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection("RabbitMq"))
            .ValidateOnStart();

        services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
        services.AddSingleton<IRabbitMqPublisher, RabbitMqPublisher>();

        services.AddHostedService<ProductDeletedConsumer>();
        services.AddHostedService<ProductVerifyConsumer>();
        services.AddHostedService<createVerifyObjConsumer>();
        services.AddHostedService<ProductUnverifyConsumer>();

        return services;
    }
}

