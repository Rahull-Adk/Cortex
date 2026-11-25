using Cortex.Application.Common;

namespace Cortex.Application.Documents;

public static class DocumentErrors
{
    public static readonly Error NotFound = Error.NotFound("Document not found.");
    public static readonly Error UploadNotReceived = Error.Conflict(
        "The file has not reached storage yet. Finish the upload, then retry.");
    public static readonly Error UploadMismatch = Error.Validation(
        "The uploaded file does not match the size declared when the upload started. Start a new upload.");
    public static readonly Error UploadFailed = Error.Conflict("This upload failed. Start a new upload.");
    public static readonly Error NotDownloadable = Error.Conflict("This document has no uploaded file yet.");
    public static readonly Error ConcurrentChange = Error.Conflict(
        "The document changed at the same time. Reload and retry.");
}
