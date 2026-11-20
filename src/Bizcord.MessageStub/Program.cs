using System.Reflection;
using Bizcord.MessageClient;
using Bizcord.MessageStub;
using Bizcord.Vault;

// A stand-in for the Message service, so the upload saga can run end to end. No HTTP and no database.
var builder = Host.CreateApplicationBuilder(args);

if (builder.Configuration["VAULT_TOKEN"] is not null)
{
    await builder.Configuration.AddVaultSecretsAsync("message-stub");
}

builder.Services.Configure<MessageStubOptions>(builder.Configuration.GetSection("MessageStub"));
builder.Services.AddSingleton<PostedMessages>();
builder.Services.AddSingleton(TimeProvider.System);

// Registers PostMessageHandler and subscribes a worker for PostMessage
builder.Services.AddMessaging(builder.Configuration, Assembly.GetExecutingAssembly());

await builder.Build().RunAsync();
