using Amazon.S3;
using Amazon.S3.Model;
using ImagePipeline.Storage;
using NSubstitute;

namespace ImagePipeline.Tests.Storage;

public class R2PresignedUrlProviderTests
{
    private static R2Options CreateOptions() => new()
    {
        BucketName = "image-pipeline",
        ServiceUrl = "https://example.r2.cloudflarestorage.com",
        AccessKeyId = "unused-in-these-tests",
        SecretAccessKey = "unused-in-these-tests",
    };

    [Fact]
    public void GetPresignedUploadUrl_RequestsPutVerb_ForBucketAndKey_WithFutureExpiry()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        GetPreSignedUrlRequest? capturedRequest = null;
        s3Client.GetPreSignedURL(Arg.Do<GetPreSignedUrlRequest>(r => capturedRequest = r))
            .Returns("https://example.com/uploads/sample.jpg?signed=upload");

        var provider = new R2PresignedUrlProvider(s3Client, CreateOptions());
        var url = provider.GetPresignedUploadUrl("uploads/sample.jpg");

        Assert.Equal("https://example.com/uploads/sample.jpg?signed=upload", url.ToString());
        Assert.NotNull(capturedRequest);
        Assert.Equal("image-pipeline", capturedRequest!.BucketName);
        Assert.Equal("uploads/sample.jpg", capturedRequest.Key);
        Assert.Equal(HttpVerb.PUT, capturedRequest.Verb);

        // Expires is a placeholder (see R2PresignedUrlProvider) — this just
        // confirms it's actually set to something in the future, not that
        // 15 minutes specifically is correct.
        Assert.True(capturedRequest.Expires > DateTime.UtcNow);
    }

    [Fact]
    public void GetPresignedDownloadUrl_RequestsGetVerb_ForBucketAndKey()
    {
        var s3Client = Substitute.For<IAmazonS3>();
        GetPreSignedUrlRequest? capturedRequest = null;
        s3Client.GetPreSignedURL(Arg.Do<GetPreSignedUrlRequest>(r => capturedRequest = r))
            .Returns("https://example.com/uploads/sample.jpg?signed=download");

        var provider = new R2PresignedUrlProvider(s3Client, CreateOptions());
        var url = provider.GetPresignedDownloadUrl("uploads/sample.jpg");

        Assert.Equal("https://example.com/uploads/sample.jpg?signed=download", url.ToString());
        Assert.NotNull(capturedRequest);
        Assert.Equal("image-pipeline", capturedRequest!.BucketName);
        Assert.Equal("uploads/sample.jpg", capturedRequest.Key);
        Assert.Equal(HttpVerb.GET, capturedRequest.Verb);
    }
}
