using System.Net;
using System.Net.Http.Json;
using System.Text;
using ImagePipeline.Api.Jobs;
using ImagePipeline.Core;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace ImagePipeline.Tests.Api;

// Integration tests for POST /jobs/single using WebApplicationFactory.
// IJobPublisher is replaced with an NSubstitute mock so no real RabbitMQ
// connection is attempted.
//
// Configuration injection via ConfigureAppConfiguration is deferred in
// WebApplication / .NET 10 — callbacks fire during Build(), but
// AddJobPublisher reads builder.Configuration during service registration
// (before Build()). Environment variables are read by
// WebApplication.CreateBuilder as part of the default configuration chain,
// so they ARE available when service registration runs. We set them in the
// constructor and clear them in Dispose(). __ is the env-var separator
// for nested keys (: is the in-process separator).
public sealed class JobsEndpointTests : IDisposable
{
    private readonly IJobPublisher _publisher;
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    // Minimal raw JSON for a valid single-resize submission.
    private const string ValidRequestJson = """
        {
          "assetRef": "images/hero.png",
          "recipe": {
            "operations": [
              { "$type": "resize", "params": { "width": 800, "height": 600 } }
            ],
            "outputDestination": "outputs/",
            "isRelativeDestination": true
          }
        }
        """;

    // Keys set as environment variables so WebApplication.CreateBuilder picks
    // them up before AddJobPublisher / AddR2PresignedUrlProvider run.
    private static readonly string[] EnvVarKeys =
    [
        "RabbitMq__Host", "RabbitMq__Username", "RabbitMq__Password", "RabbitMq__QueueName",
        "R2__BucketName", "R2__ServiceUrl", "R2__AccessKeyId", "R2__SecretAccessKey",
    ];

    public JobsEndpointTests()
    {
        Environment.SetEnvironmentVariable("RabbitMq__Host",          "localhost");
        Environment.SetEnvironmentVariable("RabbitMq__Username",      "test");
        Environment.SetEnvironmentVariable("RabbitMq__Password",      "test");
        Environment.SetEnvironmentVariable("RabbitMq__QueueName",     "test-queue");
        Environment.SetEnvironmentVariable("R2__BucketName",          "test-bucket");
        Environment.SetEnvironmentVariable("R2__ServiceUrl",          "https://test.r2.dev");
        Environment.SetEnvironmentVariable("R2__AccessKeyId",         "test-key-id");
        Environment.SetEnvironmentVariable("R2__SecretAccessKey",     "test-secret");

        _publisher = Substitute.For<IJobPublisher>();

        // ConfigureTestServices runs after the app's ConfigureServices;
        // the last registration of IJobPublisher wins in .NET DI, so the
        // mock replaces RabbitMqJobPublisher. Because the mock is resolved
        // instead of the real publisher, IConnection is never instantiated
        // and the factory never tries to connect to RabbitMQ.
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
                b.ConfigureTestServices(services =>
                    services.AddSingleton<IJobPublisher>(_publisher)));

        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        foreach (var key in EnvVarKeys)
            Environment.SetEnvironmentVariable(key, null);
    }

    // ─── Happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task PostJobsSingle_Returns202_WithJobId_WhenRequestIsValid()
    {
        using var content = JsonStringContent(ValidRequestJson);

        var response = await _client.PostAsync("/jobs/single", content);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JobSubmissionResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body.JobId);
    }

    [Fact]
    public async Task PostJobsSingle_PublishesEnvelopeWithCorrectFields_WhenRequestIsValid()
    {
        using var content = JsonStringContent(ValidRequestJson);

        await _client.PostAsync("/jobs/single", content);

        await _publisher.Received(1).PublishAsync(
            Arg.Is<JobEnvelope>(e =>
                e.AssetRef             == "images/hero.png"
                && e.JobType           == JobTypes.ProcessRecipe
                && e.JobId             == e.CorrelationId   // Single mode invariant
                && e.JobId             != Guid.Empty
                && e.Recipe            != null
                && e.Recipe.Operations.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // ─── Validation ───────────────────────────────────────────────────────

    [Fact]
    public async Task PostJobsSingle_Returns400_WhenAssetRefIsMissing()
    {
        const string missingAssetRef = """
            {
              "recipe": {
                "operations": [],
                "outputDestination": "outputs/",
                "isRelativeDestination": true
              }
            }
            """;

        using var content = JsonStringContent(missingAssetRef);

        var response = await _client.PostAsync("/jobs/single", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── Helper ───────────────────────────────────────────────────────────

    private static StringContent JsonStringContent(string json) =>
        new(json, Encoding.UTF8, "application/json");
}
