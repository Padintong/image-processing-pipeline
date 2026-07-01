using Microsoft.Extensions.DependencyInjection;

namespace ImagePipeline.Processing;

public static class ProcessingServiceCollectionExtensions
{
    // ImageSharpProcessor is stateless — one instance for the entire Worker
    // lifetime. ImageSharp is thread-safe for concurrent Mutate calls on
    // separate Image instances, which is what Worker's consumer loop produces
    // (ADR-029).
    public static IServiceCollection AddImageProcessing(this IServiceCollection services)
    {
        services.AddSingleton<IImageProcessor, ImageSharpProcessor>();
        return services;
    }
}
