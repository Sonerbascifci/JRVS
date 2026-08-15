using Jarvis.Core.Common;
using Jarvis.Core.Tools;

namespace Jarvis.Core.Tests;

public sealed class ToolExecutionResultTests
{
    [Fact]
    public void Succeeded_CreatesSuccessWithoutFailure()
    {
        var data = new TestResultData("value");

        var result = ToolExecutionResult.Succeeded(data, "Completed.");

        Assert.True(result.Success);
        Assert.Same(data, result.Data);
        Assert.Equal("Completed.", result.UserMessage);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void Failed_CreatesFailureWithoutSuccessData()
    {
        var result = ToolExecutionResult.Failed(
            FailureCode.PermissionDenied,
            "Trusted permission policy denied the action.",
            "Permission denied.");

        Assert.False(result.Success);
        Assert.Null(result.Data);
        Assert.Equal("Permission denied.", result.UserMessage);
        Assert.NotNull(result.Failure);
        Assert.Equal(FailureCode.PermissionDenied, result.Failure.Code);
    }

    [Fact]
    public void Failed_WhenFailureCodeIsUnknown_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ToolExecutionResult.Failed((FailureCode)999, "Failure."));
    }

    private sealed record TestResultData(string Value) : IToolResultData;
}
