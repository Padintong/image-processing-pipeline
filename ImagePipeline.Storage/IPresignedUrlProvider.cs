namespace ImagePipeline.Storage;

// Api's only storage-side capability (ADR-022): minting presigned URLs so
// clients upload/download directly against R2, never proxying bytes through
// Api itself. Split from IObjectStorage (ADR-024) because Api never performs
// real object I/O and should hold no credentials that could — these two
// interfaces are meant to back separately-scoped AmazonS3Client credentials.
//
// Both methods are synchronous on purpose: generating a presigned URL
// (AmazonS3Client.GetPreSignedURL) is local request signing, not a network
// call, so there is nothing to await. That's also why this stays a plain
// interface rather than mirroring EnvelopeMapper's static-class shape — the
// implementation still holds a long-lived, credentialed AmazonS3Client and
// needs to be DI'd and mockable in tests, even though no individual call
// does I/O.
public interface IPresignedUrlProvider
{
    Uri GetPresignedUploadUrl(string assetRef);

    Uri GetPresignedDownloadUrl(string assetRef);
}
