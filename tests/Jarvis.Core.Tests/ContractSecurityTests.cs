using Jarvis.Core.AI;
using Jarvis.Core.Audio;
using Jarvis.Core.Permissions;
using Jarvis.Core.Tools;

namespace Jarvis.Core.Tests;

public sealed class ContractSecurityTests
{
    public static TheoryData<Type, string> AsyncContracts => new()
    {
        { typeof(ILlmProvider), nameof(ILlmProvider.GenerateAsync) },
        { typeof(ISpeechToTextProvider), nameof(ISpeechToTextProvider.TranscribeAsync) },
        { typeof(ITextToSpeechProvider), nameof(ITextToSpeechProvider.SpeakAsync) },
        { typeof(IWakeWordDetector), nameof(IWakeWordDetector.WaitForWakeWordAsync) },
        { typeof(IJarvisTool), nameof(IJarvisTool.ExecuteAsync) },
        { typeof(IUserConfirmationService), nameof(IUserConfirmationService.RequestConfirmationAsync) }
    };

    [Theory]
    [MemberData(nameof(AsyncContracts))]
    public void AsyncOperation_RequiresCancellationToken(Type contractType, string methodName)
    {
        var method = contractType.GetMethod(methodName);

        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        Assert.NotEmpty(parameters);
        Assert.Equal(typeof(CancellationToken), parameters[^1].ParameterType);
        Assert.True(typeof(Task).IsAssignableFrom(method.ReturnType));
    }

    [Theory]
    [InlineData("RiskLevel")]
    [InlineData("RequiresConfirmation")]
    [InlineData("PermissionOverride")]
    public void ToolCallRequest_DoesNotExposeModelControlledPermissionFields(string propertyName)
    {
        Assert.Null(typeof(ToolCallRequest).GetProperty(propertyName));
    }

    [Theory]
    [InlineData("RiskLevel")]
    [InlineData("RequiresConfirmation")]
    [InlineData("PermissionOverride")]
    public void LlmToolDefinition_DoesNotExposeSecurityPolicyFields(string propertyName)
    {
        Assert.Null(typeof(LlmToolDefinition).GetProperty(propertyName));
    }

    [Fact]
    public void ToolCallRequest_UsesTypedArgumentsContract()
    {
        var property = typeof(ToolCallRequest).GetProperty(nameof(ToolCallRequest.Arguments));

        Assert.NotNull(property);
        Assert.Equal(typeof(IToolArguments), property!.PropertyType);
    }

    [Fact]
    public void ConfirmationRequest_WhenActionFingerprintIsEmpty_Throws()
    {
        var descriptor = new ToolDescriptor(
            "test_tool",
            "Test tool.",
            ToolRiskLevel.Confirm,
            typeof(TestArguments));
        var context = new ToolExecutionContext(
            "request-1",
            "session-1",
            new TestArguments("value"));

        Assert.Throws<ArgumentException>(
            () => new ConfirmationRequest(
                descriptor,
                context,
                " ",
                "Perform the test action.",
                new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void CoreAssembly_DoesNotReferenceImplementationProjects()
    {
        string[] forbiddenProjects =
        [
            "Jarvis.AI",
            "Jarvis.Audio",
            "Jarvis.Desktop",
            "Jarvis.Persistence",
            "Jarvis.Tools.Developer",
            "Jarvis.Tools.Windows"
        ];
        var references = typeof(ILlmProvider)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        var forbiddenReference = references.FirstOrDefault(
            reference => forbiddenProjects.Contains(reference, StringComparer.Ordinal));

        Assert.Null(forbiddenReference);
    }

    private sealed record TestArguments(string Value) : IToolArguments;
}
