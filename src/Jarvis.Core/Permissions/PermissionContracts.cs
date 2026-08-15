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

/// <summary>
/// Trusted user-interface boundary. Model output must never be converted directly
/// into a <see cref="ConfirmationResult"/>.
/// </summary>
public interface IUserConfirmationService
{
    Task<ConfirmationResult> RequestConfirmationAsync(
        ConfirmationRequest request,
        CancellationToken cancellationToken);
}
