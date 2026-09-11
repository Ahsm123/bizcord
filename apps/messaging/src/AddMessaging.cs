using Bizcord.Messaging.Clients;
using EasyNetQ;

namespace Bizcord.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Messaging");
        services.AddEasyNetQ(connection);
        services.AddSingleton<IMessageClient, MessageClient>();
        return services;
    }
}