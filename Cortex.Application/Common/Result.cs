namespace Cortex.Application.Common;

public class Result
{
    protected Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    public static Result Success() => new(true, null, null);
    public static Result Failure(string errorCode, string errorMessage) => new(false, errorCode, errorMessage);

    public static Result<T> Success<T>(T value) => new(value);
    public static Result<T> Failure<T>(string errorCode, string errorMessage) => new(errorCode, errorMessage);
}

public class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T value) : base(true, null, null) => _value = value;
    internal Result(string errorCode, string errorMessage) : base(false, errorCode, errorMessage) { }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access Value on a failed result.");
}
