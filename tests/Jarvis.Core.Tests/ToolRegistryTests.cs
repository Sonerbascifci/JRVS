using Jarvis.Core.Common;
using Jarvis.Core.Tools;

namespace Jarvis.Core.Tests;

public sealed class ToolRegistryTests
{
    [Fact]
    public void Resolve_WhenToolIsRegistered_ReturnsSameInstanceAndTrustedDescriptor()
    {
        var tool = CreateTool("safe_tool", ToolRiskLevel.Safe);
        var registry = new ToolRegistry([tool]);

        var result = registry.Resolve("safe_tool");

        Assert.True(result.Success);
        Assert.Same(tool, result.Tool);
        Assert.Equal(ToolRiskLevel.Safe, result.Tool?.Descriptor.RiskLevel);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void Constructor_WithMultipleTools_ExposesDescriptorsInRegistrationOrder()
    {
        var safeTool = CreateTool("safe_tool", ToolRiskLevel.Safe);
        var confirmTool = CreateTool("confirm_tool", ToolRiskLevel.Confirm);

        var registry = new ToolRegistry([safeTool, confirmTool]);

        Assert.Equal(
            ["safe_tool", "confirm_tool"],
            registry.Descriptors.Select(descriptor => descriptor.Name).ToArray());
        Assert.Equal(ToolRiskLevel.Confirm, registry.Descriptors[1].RiskLevel);
    }

    [Fact]
    public void Resolve_WhenToolIsUnknown_ReturnsControlledNotFoundFailure()
    {
        var registry = new ToolRegistry([CreateTool("known_tool", ToolRiskLevel.Safe)]);

        var result = registry.Resolve("does_not_exist");

        Assert.False(result.Success);
        Assert.Null(result.Tool);
        Assert.Equal(FailureCode.NotFound, result.Failure?.Code);
    }

    [Fact]
    public void Constructor_WhenToolNameIsDuplicated_ThrowsDeterministically()
    {
        var first = CreateTool("duplicate_tool", ToolRiskLevel.Safe);
        var second = CreateTool("duplicate_tool", ToolRiskLevel.Confirm);

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ToolRegistry([first, second]));

        Assert.Equal("A tool named 'duplicate_tool' is already registered.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Resolve_WhenToolNameIsMissing_ReturnsInvalidArguments(string? toolName)
    {
        var registry = new ToolRegistry([]);

        var result = registry.Resolve(toolName);

        Assert.False(result.Success);
        Assert.Equal(FailureCode.InvalidArguments, result.Failure?.Code);
    }

    [Fact]
    public void Resolve_UsesOrdinalCaseSensitiveNames()
    {
        var registry = new ToolRegistry([CreateTool("safe_tool", ToolRiskLevel.Safe)]);

        var result = registry.Resolve("SAFE_TOOL");

        Assert.False(result.Success);
        Assert.Equal(FailureCode.NotFound, result.Failure?.Code);
    }

    [Fact]
    public void Descriptors_CannotBeMutatedByCaller()
    {
        var registry = new ToolRegistry([CreateTool("safe_tool", ToolRiskLevel.Safe)]);
        var mutableView = Assert.IsAssignableFrom<IList<ToolDescriptor>>(registry.Descriptors);

        Assert.Throws<NotSupportedException>(() => mutableView.Clear());
        Assert.Single(registry.Descriptors);
    }

    [Fact]
    public void Constructor_WhenRegisteredCollectionContainsNullTool_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ToolRegistry([null!]));
    }

    [Fact]
    public void Constructor_WhenRegisteredToolReturnsNullDescriptor_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => new ToolRegistry([new NullDescriptorTool()]));
    }

    [Fact]
    public void ResolveContract_DoesNotAcceptCallerSuppliedRiskOrPermissionData()
    {
        var method = typeof(IToolRegistry).GetMethod(nameof(IToolRegistry.Resolve));

        var parameter = Assert.Single(method?.GetParameters() ?? []);
        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.Equal("toolName", parameter.Name);
    }

    private static FakeTool CreateTool(string name, ToolRiskLevel riskLevel) =>
        new(
            new ToolDescriptor(
                name,
                $"Description for {name}.",
                riskLevel,
                typeof(FakeArguments)));

    private sealed class FakeTool(ToolDescriptor descriptor) : IJarvisTool
    {
        public ToolDescriptor Descriptor { get; } = descriptor;

        public Task<ToolExecutionResult> ExecuteAsync(
            ToolExecutionContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Registry tests must not execute tools.");
    }

    private sealed class NullDescriptorTool : IJarvisTool
    {
        public ToolDescriptor Descriptor => null!;

        public Task<ToolExecutionResult> ExecuteAsync(
            ToolExecutionContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Registry tests must not execute tools.");
    }

    private sealed record FakeArguments(string Value) : IToolArguments;
}
