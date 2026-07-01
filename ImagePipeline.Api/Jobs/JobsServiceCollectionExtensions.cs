using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace ImagePipeline.Api.Jobs;

public static class JobsServiceCollectionExtensions
{
    public static IServiceCollection AddJobPublisher(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = BindRabbitMqOptions(configuration);
        services.AddSingleton(options);

        // IConnection is a singleton: one TCP connection per process, shared
        // across all publish calls. RabbitMqJobPublisher opens a new channel
        // per publish (channels are not thread-safe; per-call creation is the
        // simplest correct approach for the current single-job submission path).
        // CreateConnectionAsync is the v7 async API; GetAwaiter().GetResult()
        // is acceptable here because DI factories are synchronous and this
        // runs exactly once at startup.
        services.AddSingleton<IConnection>(_ =>
        {
            var factory = new ConnectionFactory
            {
                HostName = options.Host,
                Port     = options.Port,
                UserName = options.Username,
                Password = options.Password,
            };
            return factory.CreateConnectionAsync().GetAwaiter().GetResult();
        });

        services.AddSingleton<IJobPublisher, RabbitMqJobPublisher>();

        return services;
    }

    private static RabbitMqOptions BindRabbitMqOptions(IConfiguration configuration)
    {
        var section = configuration.GetSection(RabbitMqOptions.SectionName);
        return new RabbitMqOptions
        {
            Host = section[nameof(RabbitMqOptions.Host)]
                ?? throw new InvalidOperationException("RabbitMq:Host is not configured."),
            Port = int.TryParse(section[nameof(RabbitMqOptions.Port)], out var port) ? port : 5672,
            Username = section[nameof(RabbitMqOptions.Username)]
                ?? throw new InvalidOperationException(
                    "RabbitMq:Username is not configured. " +
                    "Set it via 'dotnet user-secrets set RabbitMq:Username <value>'."),
            Password = section[nameof(RabbitMqOptions.Password)]
                ?? throw new InvalidOperationException(
                    "RabbitMq:Password is not configured. " +
                    "Set it via 'dotnet user-secrets set RabbitMq:Password <value>'."),
            QueueName = section[nameof(RabbitMqOptions.QueueName)]
                ?? throw new InvalidOperationException("RabbitMq:QueueName is not configured."),
        };
    }
}
