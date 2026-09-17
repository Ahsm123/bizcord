using Bizcord.MessageClient.Clients;
using EasyNetQ;

namespace Bizcord.MessageClient;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Messaging");
        services.AddEasyNetQ(connection);
        services.AddSingleton<IMessageClient, Clients.MessageClient>();
        return services;
    }
}