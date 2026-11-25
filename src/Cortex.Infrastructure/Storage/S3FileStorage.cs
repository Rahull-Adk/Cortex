using System.Net;
using System.Net.Http.Headers;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Cortex.Application.Interfaces;
using Cortex.Infrastructure.Storage.Settings;
using Microsoft.Extensions.Options;

namespace Cortex.Infrastructure.Storage;

public sealed class S3FileStorage : IFileStorage, IDisposable
{
    private readonly S3Settings _settings;
    private readonly Lazy<AmazonS3Client> _client;
    private readonly Lazy<AmazonS3Client> _signer;

    public S3FileStorage(IOptions<S3Settings> settings)
    {
        _settings = settings.Value;

        // Lazy so the API still starts in environments without storage configured.
        _client = new Lazy<AmazonS3Client>(() => CreateClient(_settings.ServiceUrl));
        _signer = new Lazy<AmazonS3Client>(() => string.IsNullOrWhiteSpace(_settings.PublicServiceUrl)
            ? _client.Value
            : CreateClient(_settings.PublicServiceUrl));
    }

    private string Bucket => Required(_settings.BucketName, "S3:BucketName");

    public async Task<PresignedUrl> CreateUploadUrlAsync(
        string key, string contentType, long sizeBytes, CancellationToken ct = default)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.UploadUrlExpiryMinutes);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key,
            Verb = HttpVerb.PUT,
            ContentType = contentType,
            Expires = expiresAt,
            Protocol = SignerProtocol,
        };
        request.Headers.ContentLength = sizeBytes;

        return new PresignedUrl(await _signer.Value.GetPreSignedURLAsync(request), expiresAt);
    }

    public async Task<PresignedUrl> CreateDownloadUrlAsync(string key, string fileName, CancellationToken ct = default)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.DownloadUrlExpiryMinutes);
        var request = new GetPreSignedUrlRequest
        {
            BucketName = Bucket,
            Key = key,
            Verb = HttpVerb.GET,
            Expires = expiresAt,
            Protocol = SignerProtocol,
        };
        request.ResponseHeaderOverrides.ContentDisposition =
            new ContentDispositionHeaderValue("attachment") { FileNameStar = fileName }.ToString();

        return new PresignedUrl(await _signer.Value.GetPreSignedURLAsync(request), expiresAt);
    }

    public async Task<StoredObject?> GetObjectAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var metadata = await _client.Value.GetObjectMetadataAsync(Bucket, key, ct);
            return new StoredObject(metadata.Headers.ContentLength, metadata.Headers.ContentType);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task DeleteAsync(string key, CancellationToken ct = default) =>
        _client.Value.DeleteObjectAsync(Bucket, key, ct);

    public void Dispose()
    {
        if (_signer.IsValueCreated && !ReferenceEquals(_signer.Value, _client.Value))
            _signer.Value.Dispose();
        if (_client.IsValueCreated)
            _client.Value.Dispose();
    }

    private Protocol SignerProtocol =>
        (string.IsNullOrWhiteSpace(_settings.PublicServiceUrl) ? _settings.ServiceUrl : _settings.PublicServiceUrl)
            ?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true
            ? Protocol.HTTP
            : Protocol.HTTPS;

    private AmazonS3Client CreateClient(string? serviceUrl)
    {
        var config = new AmazonS3Config();
        if (string.IsNullOrWhiteSpace(serviceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(Required(_settings.Region, "S3:Region"));
        }
        else
        {
            config.ServiceURL = serviceUrl;
            config.AuthenticationRegion = _settings.Region;
            config.ForcePathStyle = true;
        }

        if (string.IsNullOrWhiteSpace(_settings.AccessKeyId) || string.IsNullOrWhiteSpace(_settings.SecretAccessKey))
            return new AmazonS3Client(config);

        return new AmazonS3Client(new BasicAWSCredentials(_settings.AccessKeyId, _settings.SecretAccessKey), config);
    }

    private static string Required(string? value, string name) =>
        string.IsNullOrWhiteSpace(value) ? throw new InvalidOperationException($"{name} is not configured.") : value;
}
