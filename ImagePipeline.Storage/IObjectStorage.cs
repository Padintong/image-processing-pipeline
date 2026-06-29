namespace ImagePipeline.Storage;

// Worker's storage-side capability (ADR-022): actual object I/O against R2 —
// reading the source asset and writing the recipe's output. Split from
// IPresignedUrlProvider (ADR-024) because Worker, unlike Api, needs
// read/write credentials rather than mint-only ones.
//
// Stream-based rather than byte[]-based so a large image is never fully
// buffered in memory just to move it through this interface, and so the
// returned Stream composes directly with ImageSharp's Image.Load(Stream)
// once the processing step exists.
//
// assetRef is a plain string, matching JobEnvelope.AssetRef and the wire's
// asset_ref field (ADR-016/017) — no dedicated reference type exists
// anywhere else in the codebase, so introducing one here would be
// inconsistent rather than safer.
public interface IObjectStorage
{
    Task<Stream> GetObjectAsync(string assetRef, CancellationToken cancellationToken = default);

    Task PutObjectAsync(string assetRef, Stream content, CancellationToken cancellationToken = default);
}
