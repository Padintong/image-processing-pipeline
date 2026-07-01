namespace ImagePipeline.Api.Jobs;

// Config POCO bound from the "RabbitMq" section of each service's
// configuration (appsettings.json + User Secrets / k8s Secret).
// Host/QueueName come from appsettings; Username/Password come from
// User Secrets locally and a Kubernetes Secret in the cluster.
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public required string Host      { get; init; }

    // Default 5672 is the standard AMQP port; overriding is rarely needed
    // but kept configurable so the k8s service port can diverge if required.
    public int Port { get; init; } = 5672;

    public required string Username  { get; init; }
    public required string Password  { get; init; }
    public required string QueueName { get; init; }
}
