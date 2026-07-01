using ImagePipeline.Core;

namespace ImagePipeline.Api.Jobs;

// Abstracts the act of publishing a fully-formed JobEnvelope to the broker
// so that Api controllers/endpoints never import RabbitMQ.Client types
// directly — analogous to how IObjectStorage / IPresignedUrlProvider hide
// IAmazonS3 behind a seam (ADR-024 pattern).
public interface IJobPublisher
{
    Task PublishAsync(JobEnvelope envelope, CancellationToken ct = default);
}
