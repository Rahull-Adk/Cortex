namespace Cortex.Application.Common;

public sealed class UniqueConstraintException(Exception innerException)
    : Exception("A unique constraint was violated.", innerException);
