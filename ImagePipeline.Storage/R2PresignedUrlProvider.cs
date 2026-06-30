using Amazon.S3;
using Amazon.S3.Model;

namespace ImagePipeline.Storage;

// Api's side of the storage boundary (ADR-022/025/028): mints presigned
// URLs only. This class has no code path capable of GetObject/PutObject —
// that capability lives entirely in R2ObjectStorage, a separate class, so
// the Api/Worker boundary is enforced by what each type can do, not by
// which interface it happens to be registered against.
public sealed class R2PresignedUrlProvider : IPresignedUrlProvider
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public R2PresignedUrlProvider(IAmazonS3 s3Client, R2Options options)
    {
        _s3Client = s3Client;
        _bucketName = options.BucketName;
    }

    public Uri GetPresignedUploadUrl(string assetRef) => GetPresignedUrl(assetRef, HttpVerb.PUT);

    public Uri GetPresignedDownloadUrl(string assetRef) => GetPresignedUrl(assetRef, HttpVerb.GET);

    private Uri GetPresignedUrl(string assetRef, HttpVerb verb)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = assetRef,
            Verb = verb,

            // Placeholder expiry. The real policy belongs to the still-
            // informal R2 cost-exposure mitigation set (ADR-027 references
            // it; not yet its own ADR) — 15 minutes is a starting guess,
            // not a decision.
            Expires = DateTime.UtcNow.AddMinutes(15),
        };

        return new Uri(_s3Client.GetPreSignedURL(request));
    }
}
