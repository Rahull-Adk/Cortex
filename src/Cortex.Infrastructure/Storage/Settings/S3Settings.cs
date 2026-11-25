namespace Cortex.Infrastructure.Storage.Settings;

public class S3Settings
{
    public string BucketName { get; set; } = default!;
    public string Region { get; set; } = "us-east-1";

    // Set only for S3-compatible stores such as MinIO. Leave empty for AWS.
    public string? ServiceUrl { get; set; }

    // The address browsers reach the store at, when it differs from ServiceUrl (e.g. inside Docker).
    public string? PublicServiceUrl { get; set; }

    // Leave empty to use the default AWS credential chain (environment, profile, instance role).
    public string? AccessKeyId { get; set; }
    public string? SecretAccessKey { get; set; }

    public int UploadUrlExpiryMinutes { get; set; } = 15;
    public int DownloadUrlExpiryMinutes { get; set; } = 5;
}
