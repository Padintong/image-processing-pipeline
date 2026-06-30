using ImagePipeline.Storage;
using ImagePipeline.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Worker is the only service that performs real object I/O (ADR-022/025/028)
// — it never sees IPresignedUrlProvider/R2PresignedUrlProvider registered
// in this container.
builder.Services.AddR2ObjectStorage(builder.Configuration);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
