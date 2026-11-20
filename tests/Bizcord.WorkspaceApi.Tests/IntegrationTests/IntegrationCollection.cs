namespace Bizcord.WorkspaceApi.Tests.IntegrationTests;

// Every integration test that starts WorkspaceApi with the real RabbitMQ and Postgres is in this collection, so they
// run one at a time. Otherwise their hosts compete for the same MessagePostedEvent queue and outbox rows (#66): a
// message from one test could be handled by another test's host, whose outbox publisher is turned off.
[CollectionDefinition(Name)]
public class IntegrationCollection
{
    public const string Name = "WorkspaceApi integration";
}
