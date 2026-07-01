namespace ImagePipeline.Api.Jobs;

// 202 Accepted response body for POST /jobs/single.
// The client uses JobId to poll for status or correlate async events.
// Single mode: correlation_id == job_id (set by the endpoint handler).
public sealed record JobSubmissionResponse(Guid JobId);
