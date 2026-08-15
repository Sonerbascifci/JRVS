using Jarvis.Core.Common;
using Jarvis.Core.Permissions;
using Jarvis.Core.Tools;

namespace Jarvis.Core.Tests;

public sealed class PermissionSecurityTests
{
    private static readonly DateTimeOffset CurrentTime =
        new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SafeTool_WhenEvaluated_ReturnsAllowed()
    {
        var decision = Evaluate(ToolRiskLevel.Safe);

        Assert.Equal(PermissionDecision.Allow, decision);
    }

    [Fact]
    public void ConfirmTool_WhenEvaluated_ReturnsConfirmationRequired()
    {
        var decision = Evaluate(ToolRiskLevel.Confirm);

        Assert.Equal(PermissionDecision.RequireConfirmation, decision);
    }

    [Fact]
    public void ConfirmTool_DoesNotReturnAllowedBeforeConfirmation()
    {
        var decision = Evaluate(ToolRiskLevel.Confirm);

        Assert.NotEqual(PermissionDecision.Allow, decision);
    }

    [Fact]
    public void CriticalTool_WhenEvaluated_ReturnsDenied()
    {
        var decision = Evaluate(ToolRiskLevel.Critical);

        Assert.Equal(PermissionDecision.Deny, decision);
    }

    [Theory]
    [InlineData(ToolRiskLevel.Confirm, PermissionDecision.RequireConfirmation)]
    [InlineData(ToolRiskLevel.Critical, PermissionDecision.Deny)]
    public void CallerSuppliedRiskAndConfirmation_DoNotOverrideTrustedDescriptor(
        ToolRiskLevel trustedRisk,
        PermissionDecision expectedDecision)
    {
        var descriptor = CreateDescriptor("trusted_tool", trustedRisk);
        var context = new ToolExecutionContext(
            "request-1",
            "session-1",
            new OverrideAttemptArguments(ToolRiskLevel.Safe, Confirmed: true));
        var evaluator = new PermissionEvaluator();

        var decision = evaluator.Evaluate(descriptor, context);

        Assert.Equal(expectedDecision, decision);
    }

    [Fact]
    public void ExactApprovedUnexpiredConfirmation_WhenValidated_ReturnsValid()
    {
        var request = CreateConfirmationRequest(expiresAt: CurrentTime.AddMinutes(1));
        var response = CreateResponse(request, ConfirmationResult.Approved);

        var result = CreateValidator().Validate(request, response);

        Assert.True(result.IsValid);
        Assert.Null(result.Failure);
    }

    [Theory]
    [InlineData(ConfirmationResult.Rejected)]
    [InlineData(ConfirmationResult.Expired)]
    [InlineData(ConfirmationResult.Cancelled)]
    public void NonApprovedConfirmation_WhenValidated_ReturnsPermissionDenied(
        ConfirmationResult confirmationResult)
    {
        var request = CreateConfirmationRequest(expiresAt: CurrentTime.AddMinutes(1));
        var response = CreateResponse(request, confirmationResult);

        var result = CreateValidator().Validate(request, response);

        Assert.False(result.IsValid);
        Assert.Equal(FailureCode.PermissionDenied, result.Failure?.Code);
    }

    [Fact]
    public void ExpiredConfirmation_WhenValidated_ReturnsPermissionDenied()
    {
        var request = CreateConfirmationRequest(expiresAt: CurrentTime.AddTicks(-1));
        var response = CreateResponse(request, ConfirmationResult.Approved);

        var result = CreateValidator().Validate(request, response);

        Assert.False(result.IsValid);
        Assert.Equal(FailureCode.PermissionDenied, result.Failure?.Code);
    }

    [Fact]
    public void ConfirmationExpiringNow_WhenValidated_ReturnsPermissionDenied()
    {
        var request = CreateConfirmationRequest(expiresAt: CurrentTime);
        var response = CreateResponse(request, ConfirmationResult.Approved);

        var result = CreateValidator().Validate(request, response);

        Assert.False(result.IsValid);
        Assert.Equal(FailureCode.PermissionDenied, result.Failure?.Code);
    }

    [Fact]
    public void ConfirmationWithWrongRequestId_WhenValidated_ReturnsPermissionDenied()
    {
        var request = CreateConfirmationRequest(expiresAt: CurrentTime.AddMinutes(1));
        var response = new ConfirmationResponse(
            ConfirmationResult.Approved,
            "different-request",
            request.Descriptor.Name,
            request.ActionFingerprint);

        AssertDenied(request, response);
    }

    [Fact]
    public void ConfirmationWithWrongToolName_WhenValidated_ReturnsPermissionDenied()
    {
        var request = CreateConfirmationRequest(expiresAt: CurrentTime.AddMinutes(1));
        var response = new ConfirmationResponse(
            ConfirmationResult.Approved,
            request.Context.RequestId,
            "different_tool",
            request.ActionFingerprint);

        AssertDenied(request, response);
    }

    [Fact]
    public void ConfirmationWithWrongFingerprint_WhenValidated_ReturnsPermissionDenied()
    {
        var request = CreateConfirmationRequest(expiresAt: CurrentTime.AddMinutes(1));
        var response = new ConfirmationResponse(
            ConfirmationResult.Approved,
            request.Context.RequestId,
            request.Descriptor.Name,
            "fingerprint-b");

        AssertDenied(request, response);
    }

    [Fact]
    public void ConfirmationForActionA_WhenReplayedForActionB_ReturnsPermissionDenied()
    {
        var actionA = CreateConfirmationRequest(
            fingerprint: "fingerprint-a",
            expiresAt: CurrentTime.AddMinutes(1));
        var actionB = CreateConfirmationRequest(
            fingerprint: "fingerprint-b",
            expiresAt: CurrentTime.AddMinutes(1));
        var responseForActionA = CreateResponse(actionA, ConfirmationResult.Approved);

        AssertDenied(actionB, responseForActionA);
    }

    [Fact]
    public void ConfirmationContracts_DoNotExposeMutationSetters()
    {
        var requestSetters = typeof(ConfirmationRequest)
            .GetProperties()
            .Where(property => property.SetMethod is not null);
        var responseSetters = typeof(ConfirmationResponse)
            .GetProperties()
            .Where(property => property.SetMethod is not null);

        Assert.Empty(requestSetters);
        Assert.Empty(responseSetters);
    }

    [Fact]
    public void ConfirmationService_ReturnsBoundResponseInsteadOfBareResult()
    {
        var method = typeof(IUserConfirmationService)
            .GetMethod(nameof(IUserConfirmationService.RequestConfirmationAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<ConfirmationResponse>), method!.ReturnType);
    }

    [Fact]
    public void MissingConfirmationInput_WhenValidated_ReturnsInvalidArguments()
    {
        var request = CreateConfirmationRequest(expiresAt: CurrentTime.AddMinutes(1));
        var response = CreateResponse(request, ConfirmationResult.Approved);
        var validator = CreateValidator();

        var missingRequest = validator.Validate(null, response);
        var missingResponse = validator.Validate(request, null);

        Assert.False(missingRequest.IsValid);
        Assert.Equal(FailureCode.InvalidArguments, missingRequest.Failure?.Code);
        Assert.False(missingResponse.IsValid);
        Assert.Equal(FailureCode.InvalidArguments, missingResponse.Failure?.Code);
    }

    private static PermissionDecision Evaluate(ToolRiskLevel riskLevel)
    {
        var evaluator = new PermissionEvaluator();
        var descriptor = CreateDescriptor("test_tool", riskLevel);
        var context = new ToolExecutionContext(
            "request-1",
            "session-1",
            new OverrideAttemptArguments(riskLevel, Confirmed: false));

        return evaluator.Evaluate(descriptor, context);
    }

    private static ConfirmationValidator CreateValidator() =>
        new(new FixedTimeProvider(CurrentTime));

    private static ToolDescriptor CreateDescriptor(string name, ToolRiskLevel riskLevel) =>
        new(
            name,
            "Tool used for permission security tests.",
            riskLevel,
            typeof(OverrideAttemptArguments));

    private static ConfirmationRequest CreateConfirmationRequest(
        string requestId = "request-1",
        string toolName = "confirm_tool",
        string fingerprint = "fingerprint-a",
        DateTimeOffset? expiresAt = null)
    {
        var descriptor = CreateDescriptor(toolName, ToolRiskLevel.Confirm);
        var context = new ToolExecutionContext(
            requestId,
            "session-1",
            new OverrideAttemptArguments(ToolRiskLevel.Safe, Confirmed: true));

        return new ConfirmationRequest(
            descriptor,
            context,
            fingerprint,
            "Perform the test action.",
            expiresAt ?? CurrentTime.AddMinutes(1));
    }

    private static ConfirmationResponse CreateResponse(
        ConfirmationRequest request,
        ConfirmationResult result) =>
        new(
            result,
            request.Context.RequestId,
            request.Descriptor.Name,
            request.ActionFingerprint);

    private static void AssertDenied(
        ConfirmationRequest request,
        ConfirmationResponse response)
    {
        var result = CreateValidator().Validate(request, response);

        Assert.False(result.IsValid);
        Assert.Equal(FailureCode.PermissionDenied, result.Failure?.Code);
    }

    private sealed record OverrideAttemptArguments(
        ToolRiskLevel RiskLevel,
        bool Confirmed) : IToolArguments;

    private sealed class FixedTimeProvider(DateTimeOffset currentTime) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => currentTime;
    }
}
