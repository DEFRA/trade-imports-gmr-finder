using Amazon.S3;
using Amazon.S3.Model;
using GmrFinder.Configuration;
using Microsoft.Extensions.Options;

namespace GmrFinder.Services;

public class StorageService(
    IOptions<StorageOptions> options,
    IAmazonS3 s3Client,
    ILogger<StorageService> logger,
    TimeProvider? timeProvider = null
) : IStorageService
{
    private readonly StorageOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task TryStoreSearchResultsAsync(string content)
    {
        var bucketName = _options.SearchResultStorageBucket;
        try
        {
            var request = new PutObjectRequest
            {
                BucketName = bucketName,
                Key = CreateKey(_timeProvider.GetUtcNow().Date),
                ContentBody = content,
                ContentType = "application/json",
            };

            await s3Client.PutObjectAsync(request);
        }
        catch (AmazonS3Exception ex)
        {
            logger.LogError(ex, "Could not upload search result to {BucketName}", bucketName);
        }
    }

    internal static string CreateKey(DateTime date)
    {
        return $"{date:yyyy/MM/dd}/by-mrn/{Guid.NewGuid()}.json";
    }
}
