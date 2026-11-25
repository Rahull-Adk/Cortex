using System.Collections.Frozen;

namespace Cortex.Application.Documents;

public static class DocumentRules
{
    public const int MaxFileNameLength = 255;
    public const long MaxFileSizeBytes = 50L * 1024 * 1024;

    public static readonly FrozenSet<string> AllowedContentTypes = new[]
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
        "text/markdown",
        "text/csv",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
}
