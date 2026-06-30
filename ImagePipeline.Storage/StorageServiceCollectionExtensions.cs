using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ImagePipeline.Storage;

// Each service calls exactly one of these two methods, never both. That's
// what actually keeps Api from ever registering IObjectStorage/
// R2ObjectStorage and Worker from ever registering IPresignedUrlProvider/
// R2PresignedUrlProvider, even though both types live in this one project
// (ADR-024) and both assemblies reference it.
public static class StorageServiceCollectionExtensions
{
    public static IServiceCollection AddR2PresignedUrlProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var options = BindR2Options(configuration);
        services.AddSingleton(options);
        services.AddSingleton(R2ClientFactory.Create(options));
        services.AddSingleton<IPresignedUrlProvider, R2PresignedUrlProvider>();
        return services;
    }

    public static IServiceCollection AddR2ObjectStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var options = BindR2Options(configuration);
        services.AddSingleton(options);
        services.AddSingleton(R2ClientFactory.Create(options));
        services.AddSingleton<IObjectStorage, R2ObjectStorage>();
        return services;
    }

    private static R2Options BindR2Options(IConfiguration configuration)
    {
        var section = configuration.GetSection(R2Options.SectionName);

        return new R2Options
        {
            BucketName = section[nameof(R2Options.BucketName)]
                ?? throw new InvalidOperationException("R2:BucketName is not configured."),
            ServiceUrl = section[nameof(R2Options.ServiceUrl)]
                ?? throw new InvalidOperationException("R2:ServiceUrl is not configured."),
            AccessKeyId = section[nameof(R2Options.AccessKeyId)]
                ?? throw new InvalidOperationException(
                    "R2:AccessKeyId is not configured. Set it via 'dotnet user-secrets set R2:AccessKeyId <value>'."),
            SecretAccessKey = section[nameof(R2Options.SecretAccessKey)]
                ?? throw new InvalidOperationException(
                    "R2:SecretAccessKey is not configured. Set it via 'dotnet user-secrets set R2:SecretAccessKey <value>'."),
        };
    }
}
