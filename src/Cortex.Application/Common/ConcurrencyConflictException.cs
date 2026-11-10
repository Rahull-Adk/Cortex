namespace Cortex.Application.Common;

public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The data was modified by another request.", innerException);
