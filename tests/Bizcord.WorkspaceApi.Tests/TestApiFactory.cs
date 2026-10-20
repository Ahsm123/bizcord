using Bizcord.MessageClient.Clients;
using Bizcord.WorkspaceApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Bizcord.WorkspaceApi.Tests;

// Runs the API in memory with the fake repository and message client,
// so HTTP tests don't need Postgres or RabbitMQ
public class TestApiFactory : WebApplicationFactory<Program>
{
    public FakeWorkspaceRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IWorkspaceRepository>();
            services.AddSingleton<IWorkspaceRepository>(Repository);
            services.RemoveAll<IMessageClient>();
            services.AddSingleton<IMessageClient, FakeMessageClient>();
        });
    }
}
