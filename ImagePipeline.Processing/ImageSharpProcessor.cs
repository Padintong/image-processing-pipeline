using ImagePipeline.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ImagePipeline.Processing;

// ImageSharp-backed implementation of IImageProcessor (ADR-023/029).
// Stateless — safe to register as Singleton. Swapping the underlying library
// only requires changing this class and Processing.csproj; nothing outside
// this project (Worker, Core) touches ImageSharp types directly.
//
// Only ResizeOperation is implemented in this vertical slice. All other
// OperationInstance subtypes throw NotImplementedException and will be filled
// in as those tasks are reached.
public sealed class ImageSharpProcessor : IImageProcessor
{
    // ADR-010: hard ceiling enforced before handing off to ImageSharp.
    private const int MaxDimension = 10_000;

    // Not async: dispatch is synchronous; each private helper owns the async.
    // Mirrors EnvelopeMapper's switch-expression dispatch pattern.
    public Task<Stream> ExecuteAsync(Stream input, OperationInstance operation, CancellationToken ct = default) =>
        operation switch
        {
            ResizeOperation o => ResizeAsync(input, o.Params, ct),
            _ => throw new NotImplementedException(
                $"Operation '{operation.GetType().Name}' is not yet implemented in {nameof(ImageSharpProcessor)}. " +
                "Add a case here when the corresponding task is reached."),
        };

    private static async Task<Stream> ResizeAsync(Stream input, ResizeParams p, CancellationToken ct)
    {
        // Ceiling check before ImageSharp sees the request (ADR-010).
        if (p.Width > MaxDimension)
            throw new ArgumentOutOfRangeException(nameof(p), p.Width,
                $"Resize width {p.Width} exceeds the {MaxDimension:N0} px-per-side ceiling (ADR-010).");
        if (p.Height > MaxDimension)
            throw new ArgumentOutOfRangeException(nameof(p), p.Height,
                $"Resize height {p.Height} exceeds the {MaxDimension:N0} px-per-side ceiling (ADR-010).");

        using var image = await Image.LoadAsync<Rgba32>(input, ct);

        // ResizeMode.Max = fit-within-box: scale to fill the target rectangle
        // while preserving aspect ratio, never exceeding width or height.
        // This is the invariant behavior for all Resize operations (ADR-010/012).
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(p.Width, p.Height),
            Mode = ResizeMode.Max,
        }));

        // Save as PNG for this vertical slice. Format conversion is a
        // dedicated operation (FormatConversionOperation) and is out of scope
        // here. PNG is lossless and unambiguous for output validation in tests.
        var output = new MemoryStream();
        await image.SaveAsPngAsync(output, ct);
        output.Position = 0;
        return output;
    }
}
