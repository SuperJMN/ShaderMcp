namespace ShaderMcp.Core;

public sealed class ShaderValidationException : Exception
{
    public ShaderValidationException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
