using Jarvis.Core.Tools;

namespace Jarvis.Core.Tests;

public sealed class ToolDescriptorTests
{
    [Fact]
    public void Constructor_WithValidMetadata_PreservesTrustedValues()
    {
        var descriptor = new ToolDescriptor(
            "open_application",
            "Opens a known application.",
            ToolRiskLevel.Safe,
            typeof(TestArguments));

        Assert.Equal("open_application", descriptor.Name);
        Assert.Equal("Opens a known application.", descriptor.Description);
        Assert.Equal(ToolRiskLevel.Safe, descriptor.RiskLevel);
        Assert.Equal(typeof(TestArguments), descriptor.ArgumentsType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("OpenApplication")]
    [InlineData("_open_application")]
    [InlineData("open_application_")]
    [InlineData("open__application")]
    [InlineData("open-application")]
    public void Constructor_WhenNameIsNotStableSnakeCase_Throws(string name)
    {
        Assert.Throws<ArgumentException>(
            () => new ToolDescriptor(
                name,
                "Description",
                ToolRiskLevel.Safe,
                typeof(TestArguments)));
    }

    [Fact]
    public void Constructor_WhenArgumentsTypeIsNotTypedContract_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new ToolDescriptor(
                "test_tool",
                "Description",
                ToolRiskLevel.Safe,
                typeof(string)));
    }

    [Fact]
    public void Constructor_WhenRiskLevelIsUnknown_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ToolDescriptor(
                "test_tool",
                "Description",
                (ToolRiskLevel)999,
                typeof(TestArguments)));
    }

    private sealed record TestArguments(string Value) : IToolArguments;
}
