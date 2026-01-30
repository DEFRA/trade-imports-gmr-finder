using Amazon.S3;
using Amazon.S3.Model;
using GmrFinder.Configuration;
using GmrFinder.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;

namespace GmrFinder.Tests.Services;

public class StorageServiceTests
{
    private readonly FakeTimeProvider _fakeTimeProvider = new(
        new DateTimeOffset(2024, 6, 15, 10, 30, 0, TimeSpan.Zero)
    );

    private readonly Mock<ILogger<StorageService>> _mockLogger = new();
    private readonly Mock<IAmazonS3> _mockS3Client = new();

    [Fact]
    public async Task TryStoreSearchResultsAsync_WhenFeatureEnabled_ShouldUploadToS3()
    {
        var service = CreateService("test-bucket");
        const string content = "{\"results\": []}";

        await service.TryStoreSearchResultsAsync(content);

        _mockS3Client.Verify(
            x =>
                x.PutObjectAsync(
                    It.Is<PutObjectRequest>(r =>
                        r.BucketName == "test-bucket"
                        && r.Key.StartsWith("2024/06/15/by-mrn/")
                        && r.Key.EndsWith(".json")
                        && r.ContentBody == content
                        && r.ContentType == "application/json"
                    ),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task TryStoreSearchResultsAsync_WhenS3Throws_ShouldLogErrorAndNotRethrow()
    {
        var service = CreateService("test-bucket");
        var exception = new AmazonS3Exception("S3 error");
        _mockS3Client
            .Setup(x => x.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var act = () => service.TryStoreSearchResultsAsync("{\"test\": true}");

        await act.Should().NotThrowAsync();
    }

    private StorageService CreateService(string? bucketName = null)
    {
        var storageOptions = Options.Create(new StorageOptions { SearchResultStorageBucket = bucketName });
        return new StorageService(storageOptions, _mockS3Client.Object, _mockLogger.Object, _fakeTimeProvider);
    }
}
