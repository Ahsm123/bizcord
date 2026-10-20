using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bizcord.MessageClient.Clients;
using Bizcord.MessageClient.Handlers;
using Bizcord.MessageClient.Workers;
using EasyNetQ;

namespace Bizcord.MessageClient;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, IConfiguration configuration,
        Assembly handlerAssembly)
    {
        var connection = configuration.GetConnectionString("Messaging")
                         ?? throw new InvalidOperationException("Missing ConnectionStrings:Messaging");
        services.AddEasyNetQ(connection);
        services.AddSingleton<IMessageClient, Clients.MessageClient>();
        AddMessageHandlers(services, handlerAssembly);
        return services;
    }

    // Registers every IMessageHandler<T> in the assembly and starts one MessageHandlerWorker<T> per message type.
    // The subscriber id is "<assembly name>.<message name>", so every instance of a service shares one queue per
    // message type, and each service gets its own.
    private static void AddMessageHandlers(IServiceCollection services, Assembly assembly)
    {
        var handlers = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false })
            .SelectMany(type => type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMessageHandler<>))
                .Select(handlerInterface => (handlerInterface, type)))
            .ToList();

        foreach (var (handlerInterface, implementation) in handlers)
        {
            services.AddScoped(handlerInterface, implementation);
        }

        var messageTypes = handlers.Select(h => h.handlerInterface.GetGenericArguments()[0]).Distinct();
        foreach (var messageType in messageTypes)
        {
            var workerType = typeof(MessageHandlerWorker<>).MakeGenericType(messageType);
            var subscriberId = $"{assembly.GetName().Name}.{messageType.Name}";
            services.AddSingleton<IHostedService>(sp =>
                (IHostedService)ActivatorUtilities.CreateInstance(sp, workerType, subscriberId));
        }
    }
}
