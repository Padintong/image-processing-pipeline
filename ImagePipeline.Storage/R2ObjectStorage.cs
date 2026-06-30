using Amazon.S3;
using Amazon.S3.Model;

namespace ImagePipeline.Storage;

// Worker's side of the storage boundary (ADR-022/025/028): the only type in
// this project that performs real GetObject/PutObject calls against R2.
// This class has no code path capable of minting a presigned URL — see
// R2PresignedUrlProvider for that half of the boundary.
public sealed class R2ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    public R2ObjectStorage(IAmazonS3 s3Client, R2Options options)
    {
        _s3Client = s3Client;
        _bucketName = options.BucketName;
    }

    public async Task<Stream> GetObjectAsync(string assetRef, CancellationToken cancellationToken = default)
    {
        var response = await _s3Client.GetObjectAsync(
            new GetObjectRequest { BucketName = _bucketName, Key = assetRef },
            cancellationToken);

        return response.ResponseStream;
    }

    public async Task PutObjectAsync(string assetRef, Stream content, CancellationToken cancellationToken = default)
    {
        await _s3Client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = assetRef,
                InputStream = content,

                // R2 doesn't support AWSSDK.S3's streaming SigV4 payload
                // signing on PutObject — DisablePayloadSigning forces the
                // simpler UNSIGNED-PAYLOAD signature instead (requires
                // HTTPS, which R2 always is). DisableDefaultChecksumValidation
                // is belt-and-suspenders alongside the client-level
                // WHEN_REQUIRED settings in R2ClientFactory. Both flags
                // only exist on PutObjectRequest/UploadPartRequest, not
                // on AmazonS3Config.
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
            },
            cancellationToken);
    }
}
