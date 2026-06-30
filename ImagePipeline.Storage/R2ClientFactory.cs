using Amazon.Runtime;
using Amazon.S3;

namespace ImagePipeline.Storage;

// Builds an AmazonS3Client configured for R2 rather than real S3 (ADR-022).
// Shared by R2PresignedUrlProvider and R2ObjectStorage so the five
// R2-specific AmazonS3Config settings live in exactly one place. This is
// the only thing the two classes share — everything else about them is
// deliberately separate (ADR-028).
internal static class R2ClientFactory
{
    public static IAmazonS3 Create(R2Options options)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",

            // AWSSDK.S3 v4 calculates and validates CRC-based checksums
            // by default (WHEN_SUPPORTED) on every request that supports
            // them. R2 doesn't reliably support that default checksum
            // trailer behaviour, so restrict both to WHEN_REQUIRED — only
            // send/validate a checksum when an operation strictly needs
            // one. (The separate SigV4-payload-signing workaround R2
            // needs for PutObject lives on PutObjectRequest itself, not
            // here — see R2ObjectStorage. DisablePayloadSigning/
            // DisableDefaultChecksumValidation don't exist on
            // AmazonS3Config in v4.)
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        var credentials = new BasicAWSCredentials(options.AccessKeyId, options.SecretAccessKey);
        return new AmazonS3Client(credentials, config);
    }
}
