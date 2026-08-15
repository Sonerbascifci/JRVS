using Jarvis.Core.Common;
using Jarvis.Core.Tools;

namespace Jarvis.Core.Permissions;

public enum PermissionDecision
{
    Allow,
    RequireConfirmation,
    Deny
}

public interface IPermissionEvaluator
{
    PermissionDecision Evaluate(
        ToolDescriptor descriptor,
        ToolExecutionContext context);
}

public sealed record ConfirmationRequest
{
    public ConfirmationRequest(
        ToolDescriptor descriptor,
        ToolExecutionContext context,
        string actionFingerprint,
        string actionSummary,
        DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionFingerprint);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionSummary);

        if (expiresAt == default)
        {
            throw new ArgumentException("Confirmation expiration must be specified.", nameof(expiresAt));
        }

        Descriptor = descriptor;
        Context = context;
        ActionFingerprint = actionFingerprint;
        ActionSummary = actionSummary;
        ExpiresAt = expiresAt;
    }

    public ToolDescriptor Descriptor { get; }

    public ToolExecutionContext Context { get; }

    public string ActionFingerprint { get; }

    public string ActionSummary { get; }

    public DateTimeOffset ExpiresAt { get; }
}

public enum ConfirmationResult
{
    Approved,
    Rejected,
    Expired,
    Cancelled
}

public sealed record ConfirmationResponse
{
    public ConfirmationResponse(
        ConfirmationResult result,
        string requestId,
        string toolName,
        string actionFingerprint)
    {
        if (!Enum.IsDefined(result))
        {
            throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown confirmation result.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionFingerprint);

        Result = result;
        RequestId = requestId;
        ToolName = toolName;
        ActionFingerprint = actionFingerprint;
    }

    public ConfirmationResult Result { get; }

    public string RequestId { get; }

    public string ToolName { get; }

    public string ActionFingerprint { get; }
}

public sealed record ConfirmationValidationResult
{
    private ConfirmationValidationResult(bool isValid, Failure? failure)
    {
        IsValid = isValid;
        Failure = failure;
    }

    public bool IsValid { get; }

    public Failure? Failure { get; }

    public static ConfirmationValidationResult Valid() =>
        new(isValid: true, failure: null);

    public static ConfirmationValidationResult Invalid(FailureCode code, string message) =>
        new(isValid: false, new Failure(code, message));
}

public interface IConfirmationValidator
{
    ConfirmationValidationResult Validate(
        ConfirmationRequest? request,
        ConfirmationResponse? response);
}

/// <summary>
/// Trusted user-interface boundary. Model output must never be converted directly
/// into a <see cref="ConfirmationResponse"/>.
/// </summary>
public interface IUserConfirmationService
{
    Task<ConfirmationResponse> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken);
}
