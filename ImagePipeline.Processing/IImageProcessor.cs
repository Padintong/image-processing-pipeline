using ImagePipeline.Core;

namespace ImagePipeline.Processing;

// Single unified abstraction for all image-processing operations (ADR-029).
// Mirrors the EnvelopeMapper dispatch pattern: one method, switch on the
// concrete OperationInstance subtype. Callers never depend on ImageSharp
// directly — only on this interface.
public interface IImageProcessor
{
    // Returns a caller-owned Stream positioned at offset 0. The caller is
    // responsible for disposing it. The input stream is consumed but not
    // disposed by this method.
    Task<Stream> ExecuteAsync(Stream input, OperationInstance operation, CancellationToken ct = default);
}
