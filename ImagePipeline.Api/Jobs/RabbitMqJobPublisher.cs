using ImagePipeline.Core;
using ImagePipeline.Messaging;
using RabbitMQ.Client;

namespace ImagePipeline.Api.Jobs;

// Publishes a JobEnvelope to RabbitMQ via the shared IConnection singleton.
// A new channel is opened per publish call — channels are lightweight and
// not thread-safe, so per-call creation is the simplest correct approach
// for single-job submission. A channel pool would be the upgrade path for
// high-throughput batch mode (task #153 / future).
public sealed class RabbitMqJobPublisher : IJobPublisher
{
    private readonly IConnection _connection;
    private readonly string _queueName;

    public RabbitMqJobPublisher(IConnection connection, RabbitMqOptions options)
    {
        _connection = connection;
        _queueName  = options.QueueName;
    }

    public async Task PublishAsync(JobEnvelope envelope, CancellationToken ct = default)
    {
        await using var channel = await _connection.CreateChannelAsync(cancellationToken: ct);

        // Idempotent: RabbitMQ is a no-op if the queue already exists with
        // the same parameters. Declaring here keeps the publisher self-
        // sufficient without requiring the queue to be pre-provisioned.
        await channel.QueueDeclareAsync(
            queue:       _queueName,
            durable:     true,
            exclusive:   false,
            autoDelete:  false,
            arguments:   null,
            cancellationToken: ct);

        var (properties, body) = EnvelopeMapper.ToWire(envelope);

        // Positional args avoid named-parameter name variance across
        // RabbitMQ.Client minor versions (CachedString overloads vs string).
        await channel.BasicPublishAsync("", _queueName, false, properties,
            (ReadOnlyMemory<byte>)body, ct);
    }
}
