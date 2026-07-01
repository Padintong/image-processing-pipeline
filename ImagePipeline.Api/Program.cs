using ImagePipeline.Api.Jobs;
using ImagePipeline.Core;
using ImagePipeline.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Api mints presigned URLs (ADR-022/025/028) and publishes job envelopes
// (ADR-004/017). It never registers IObjectStorage — Worker owns that side.
builder.Services.AddR2PresignedUrlProvider(builder.Configuration);
builder.Services.AddJobPublisher(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ─── POST /jobs/single ────────────────────────────────────────────────────
// Accepts a single-asset job submission (ADR-004 envelope, ADR-009 routes).
// The client provides assetRef + recipe; the server generates all envelope-
// level fields. Single mode: correlation_id == job_id (confirmed design).
app.MapPost("/jobs/single", async (
    JobSubmissionRequest request,
    IJobPublisher publisher,
    CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.AssetRef))
        return Results.Problem("assetRef is required.", statusCode: StatusCodes.Status400BadRequest);

    if (request.Recipe is null)
        return Results.Problem("recipe is required.", statusCode: StatusCodes.Status400BadRequest);

    var jobId = Guid.NewGuid();

    var envelope = new JobEnvelope(
        JobId:        jobId,
        CorrelationId: jobId,   // Single mode: correlation_id == job_id
        JobType:      JobTypes.ProcessRecipe,
        AssetRef:     request.AssetRef,
        SubmittedAt:  DateTimeOffset.UtcNow,
        Recipe:       request.Recipe);

    await publisher.PublishAsync(envelope, ct);

    return Results.Json(new JobSubmissionResponse(jobId), statusCode: StatusCodes.Status202Accepted);
});

app.Run();

// Required for WebApplicationFactory<Program> in integration tests
// (the top-level-statements Program class is internal by default).
public partial class Program { }
