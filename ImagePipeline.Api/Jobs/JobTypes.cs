namespace ImagePipeline.Api.Jobs;

// String constants for the AMQP 'Type' header (JobType in JobEnvelope /
// ADR-004/017). Kept here rather than in Core because the value catalog is
// an Api concern — Core has no knowledge of how jobs are typed on the wire.
public static class JobTypes
{
    public const string ProcessRecipe  = "process_recipe";
    public const string RegisterAsset  = "register_asset";
}
