namespace ImagePipeline.Storage;

// Plain config POCO bound from each service's own "R2" config section
// (ADR-022/026). BucketName/ServiceUrl come from that service's own
// appsettings.json; AccessKeyId/SecretAccessKey come from .NET User Secrets
// locally and a per-service Kubernetes Secret in the cluster — never
// committed alongside the rest of this section.
//
// Api and Worker each bind their own instance from their own config, even
// though the shape is identical: the values differ per service (separate
// R2 credentials, ADR-026), and each service only ever wires up the one
// adapter class it needs (see StorageServiceCollectionExtensions).
public sealed class R2Options
{
    public const string SectionName = "R2";

    public required string BucketName { get; init; }

    public required string ServiceUrl { get; init; }

    public required string AccessKeyId { get; init; }

    public required string SecretAccessKey { get; init; }
}
