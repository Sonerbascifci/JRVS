namespace Jarvis.Core.Common;

public enum FailureCode
{
    InvalidArguments,
    PermissionDenied,
    ConfirmationRequired,
    Cancelled,
    NotFound,
    Unavailable,
    ExecutionFailed,
    Timeout,
    Unsupported
}

public sealed record Failure
{
    public Failure(FailureCode code, string message)
    {
        if (!Enum.IsDefined(code))
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "Unknown failure code.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Code = code;
        Message = message;
    }

    public FailureCode Code { get; }

    public string Message { get; }
}
