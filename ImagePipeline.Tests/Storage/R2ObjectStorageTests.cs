using Amazon.S3;
using Amazon.S3.Model;
using ImagePipeline.Storage;
using NSubstitute;

namespace ImagePipeline.Tests.Storage;

public class R2ObjectStorageTests
{
    private static R2Options CreateOptions() => new()
    {
        BucketName = "image-pipeline",
        ServiceUrl = "https://example.r2.cloudflarestorage.com",
        AccessKeyId = "unused-in-these-tests",
        SecretAccessKey = "unused-in-these-tests",
    };

    [Fact]
    public async Task GetObjectAsync_ReturnsResponseStream_ForBucketAndKey()
    {
        var expectedStream = new MemoryStream("source bytes"u8.ToArray());
        var s3Client = Substitute.For<IAmazonS3>();
        GetObjectRequest? capturedRequest = null;
        s3Client.GetObjectAsync(Arg.Do<GetObjectRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new GetObjectResponse { ResponseStream = expectedStream }));

        var storage = new R2ObjectStorage(s3Client, CreateOptions());
        var stream = await storage.GetObjectAsync("uploads/sample.jpg");

        Assert.Same(expectedStream, stream);
        Assert.NotNull(capturedRequest);
        Assert.Equal("image-pipeline", capturedRequest!.BucketName);
        Assert.Equal("uploads/sample.jpg", capturedRequest.Key);
    }

    [Fact]
    public async Task PutObjectAsync_SendsContentStream_ForBucketAndKey()
    {
        var content = new MemoryStream("processed bytes"u8.ToArray());
        var s3Client = Substitute.For<IAmazonS3>();
        PutObjectRequest? capturedRequest = null;
        s3Client.PutObjectAsync(Arg.Do<PutObjectRequest>(r => capturedRequest = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PutObjectResponse()));

        var storage = new R2ObjectStorage(s3Client, CreateOptions());
        await storage.PutObjectAsync("processed/sample.jpg", content);

        Assert.NotNull(capturedRequest);
        Assert.Equal("image-pipeline", capturedRequest!.BucketName);
        Assert.Equal("processed/sample.jpg", capturedRequest.Key);
        Assert.Same(content, capturedRequest.InputStream);
    }
}
