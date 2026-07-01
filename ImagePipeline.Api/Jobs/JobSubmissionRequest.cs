using ImagePipeline.Core;

namespace ImagePipeline.Api.Jobs;

// HTTP request body for POST /jobs/single.
// The client supplies only assetRef and recipe — all envelope-level fields
// (JobId, CorrelationId, JobType, SubmittedAt) are server-generated
// (ADR-004/017). Recipe.Operations uses [JsonPolymorphic] on
// OperationInstance (Core) so System.Text.Json can deserialize each
// concrete subtype from its "$type" discriminator.
public sealed record JobSubmissionRequest(string AssetRef, Recipe Recipe);
