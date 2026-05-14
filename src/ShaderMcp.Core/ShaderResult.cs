namespace ShaderMcp.Core;

public sealed record ShaderResult(bool IsSuccess, string ErrorCode, string Error)
{
    public bool IsFailure => !IsSuccess;

    public static ShaderResult Success() => new(true, string.Empty, string.Empty);

    public static ShaderResult Failure(string errorCode, string error) => new(false, errorCode, error);
}

public sealed record ShaderResult<T>(bool IsSuccess, T Value, string ErrorCode, string Error)
{
    public bool IsFailure => !IsSuccess;

    public static ShaderResult<T> Success(T value) => new(true, value, string.Empty, string.Empty);

    public static ShaderResult<T> Failure(string errorCode, string error) => new(false, default!, errorCode, error);
}
