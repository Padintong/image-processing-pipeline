namespace ImagePipeline.Core;

// The full conceptual message envelope (ADR-004), used throughout Api/Worker
// application code as a single object. On the wire, this is split: JobId,
// CorrelationId, JobType, and SubmittedAt live in native AMQP BasicProperties;
// AssetRef and Recipe live in the Protobuf body (ADR-016, ADR-017). A
// dedicated mapper performs that split — nothing outside the mapper should
// need to know about it (ADR-018). The mapper itself, and where it should
// live, is follow-up work — not part of this pass.
public sealed record JobEnvelope(
    Guid JobId,
    Guid CorrelationId,
    string JobType,
    string AssetRef,
    DateTimeOffset SubmittedAt,
    Recipe Recipe);
