using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bizcord.Contracts.Commands;
using Bizcord.Contracts.Files;

namespace Bizcord.FileApi.Tests.ApiTests;

public class UploadEndpointsTests(TestApiFactory factory) : IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly Guid _uploader = Guid.NewGuid();

    [Fact]
    public async Task Upload_Valid_Returns202WithPendingUploadAndLocation()
    {
        // Act
        var response = await PostUploadAsync(_uploader, ValidRequest());

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var upload = await response.Content.ReadFromJsonAsync<UploadDto>();
        Assert.NotNull(upload);
        Assert.Equal("Pending", upload.Status);
        Assert.Equal("report.pdf", upload.FileName);
        Assert.EndsWith($"/api/v1/uploads/{upload.Id}", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Upload_Valid_StoresItAndAsksWorkspaceToReserveTheSize()
    {
        // Arrange
        var request = ValidRequest();

        // Act
        var upload = await (await PostUploadAsync(_uploader, request)).Content.ReadFromJsonAsync<UploadDto>();

        // Assert
        var stored = factory.Repository.uploads[upload!.Id];
        Assert.Equal(_uploader, stored.UploaderId);
        Assert.Equal("Hello", stored.Message);
        var command = Assert.Single(factory.MessageClient.Published<ReserveQuota>(), c => c.SagaId == upload.Id);
        Assert.Equal(new ReserveQuota(upload.Id, request.WorkspaceId, request.SizeBytes), command);
    }

    public static TheoryData<object> InvalidRequests => new()
    {
        new { workspaceId = Guid.Empty, channelId = Guid.NewGuid(), fileName = "a.txt", sizeBytes = 10 },
        new { workspaceId = Guid.NewGuid(), channelId = Guid.Empty, fileName = "a.txt", sizeBytes = 10 },
        new { workspaceId = Guid.NewGuid(), channelId = Guid.NewGuid(), fileName = "", sizeBytes = 10 },
        new { workspaceId = Guid.NewGuid(), channelId = Guid.NewGuid(), fileName = new string('a', 256), sizeBytes = 10 },
        new { workspaceId = Guid.NewGuid(), channelId = Guid.NewGuid(), fileName = "a.txt", sizeBytes = 0 },
        new { workspaceId = Guid.NewGuid(), channelId = Guid.NewGuid(), fileName = "a.txt", sizeBytes = -5 },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task Upload_Invalid_Returns400AndStartsNothing(object request)
    {
        // Arrange
        var before = factory.MessageClient.Published<ReserveQuota>().Count;

        // Act
        var response = await PostUploadAsync(_uploader, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(before, factory.MessageClient.Published<ReserveQuota>().Count);
    }

    [Fact]
    public async Task GetById_Uploader_Returns200WithTheUpload()
    {
        // Arrange
        var created = await (await PostUploadAsync(_uploader, ValidRequest())).Content.ReadFromJsonAsync<UploadDto>();

        // Act
        var response = await GetUploadAsync(_uploader, created!.Id);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, await response.Content.ReadFromJsonAsync<UploadDto>());
    }

    [Fact]
    public async Task GetById_AnotherUser_Returns404()
    {
        // Arrange
        var created = await (await PostUploadAsync(_uploader, ValidRequest())).Content.ReadFromJsonAsync<UploadDto>();

        // Act
        var response = await GetUploadAsync(Guid.NewGuid(), created!.Id);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_Unknown_Returns404()
    {
        // Act
        var response = await GetUploadAsync(_uploader, Guid.NewGuid());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public static TheoryData<string, string> Endpoints => new()
    {
        { "POST", "api/v1/uploads" },
        { "GET", $"api/v1/uploads/{Guid.NewGuid()}" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task NoToken_Returns401(string method, string url)
    {
        // Act
        var response = await _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = JsonContent.Create(ValidRequest())
        });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task TokenForAnotherService_Returns401(string method, string url)
    {
        // Act
        var response = await SendAsync(method, url, TestApiFactory.CreateToken("user", audience: "workspace-api"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task InternalServiceToken_Returns403(string method, string url)
    {
        // Act
        var response = await SendAsync(method, url, TestApiFactory.CreateToken("internal-service"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task SubNotAGuid_Returns403(string method, string url)
    {
        // Act
        var response = await SendAsync(method, url, TestApiFactory.CreateToken("user", sub: "bob"));

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static CreateUploadBody ValidRequest() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "report.pdf", 1024, "Hello");

    private Task<HttpResponseMessage> PostUploadAsync(Guid userId, object body) =>
        _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "api/v1/uploads")
        {
            Headers = { Authorization = TestApiFactory.UserAuthHeader(userId) },
            Content = JsonContent.Create(body)
        });

    private Task<HttpResponseMessage> GetUploadAsync(Guid userId, Guid uploadId) =>
        _client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"api/v1/uploads/{uploadId}")
        {
            Headers = { Authorization = TestApiFactory.UserAuthHeader(userId) }
        });

    private Task<HttpResponseMessage> SendAsync(string method, string url, string token) =>
        _client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) },
            Content = JsonContent.Create(ValidRequest())
        });

    private record CreateUploadBody(Guid WorkspaceId, Guid ChannelId, string FileName, long SizeBytes, string Message);
}
