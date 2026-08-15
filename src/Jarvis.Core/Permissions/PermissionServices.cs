using Jarvis.Core.Common;
using Jarvis.Core.Tools;

namespace Jarvis.Core.Permissions;

public sealed class PermissionEvaluator : IPermissionEvaluator
{
    public PermissionDecision Evaluate(
        ToolDescriptor descriptor,
        ToolExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(context);

        return descriptor.RiskLevel switch
        {
            ToolRiskLevel.Safe => PermissionDecision.Allow,
            ToolRiskLevel.Confirm => PermissionDecision.RequireConfirmation,
            ToolRiskLevel.Critical => PermissionDecision.Deny,
            _ => PermissionDecision.Deny
        };
    }
}

public sealed class ConfirmationValidator : IConfirmationValidator
{
    private readonly TimeProvider _timeProvider;

    public ConfirmationValidator(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public ConfirmationValidationResult Validate(
        ConfirmationRequest? request,
        ConfirmationResponse? response)
    {
        if (request is null || response is null)
        {
            return ConfirmationValidationResult.Invalid(
                FailureCode.InvalidArguments,
                "A confirmation request and response are required.");
        }

        if (response.Result != ConfirmationResult.Approved)
        {
            return ConfirmationValidationResult.Invalid(
                FailureCode.PermissionDenied,
                "The requested action was not approved.");
        }

        if (!string.Equals(
                request.Context.RequestId,
                response.RequestId,
                StringComparison.Ordinal)
            || !string.Equals(
                request.Descriptor.Name,
                response.ToolName,
                StringComparison.Ordinal)
            || !string.Equals(
                request.ActionFingerprint,
                response.ActionFingerprint,
                StringComparison.Ordinal))
        {
            return ConfirmationValidationResult.Invalid(
                FailureCode.PermissionDenied,
                "The confirmation does not match the requested action.");
        }

        if (request.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            return ConfirmationValidationResult.Invalid(
                FailureCode.PermissionDenied,
                "The confirmation has expired.");
        }

        return ConfirmationValidationResult.Valid();
    }
}
