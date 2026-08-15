using Jarvis.Core.AI;
using Jarvis.Core.Common;
using Jarvis.Core.Tools;

namespace Jarvis.Core.Tests;

public sealed class LlmContractTests
{
    [Fact]
    public void LlmRequest_CopiesInputCollections()
    {
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.User, "Open my project.")
        };
        var toolResults = new List<ToolCallResult>
        {
            new("call-1", ToolExecutionResult.Succeeded())
        };

        var request = new LlmRequest(messages, toolResults);
        messages.Clear();
        toolResults.Clear();

        Assert.Single(request.Messages);
        Assert.Single(request.ToolResults);
    }

    [Fact]
    public void LlmResponse_WhenContentAndToolCallsAreEmpty_Throws()
    {
        Assert.Throws<ArgumentException>(() => new LlmResponse(content: null));
    }

    [Fact]
    public void LlmResponse_WithTypedToolCall_DoesNotRequireTextContent()
    {
        var toolCall = new ToolCallRequest(
            "call-1",
            "open_application",
            new TestArguments("notepad"));

        var response = new LlmResponse(content: null, [toolCall]);

        Assert.Null(response.Content);
        Assert.Same(toolCall, Assert.Single(response.ToolCalls));
    }

    [Fact]
    public void LlmProviderResult_Succeeded_ContainsOnlyResponse()
    {
        var response = new LlmResponse("Hello.");

        var result = LlmProviderResult.Succeeded(response);

        Assert.True(result.Success);
        Assert.Same(response, result.Response);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void LlmProviderResult_Failed_ContainsOnlyFailure()
    {
        var result = LlmProviderResult.Failed(
            FailureCode.Unavailable,
            "The provider is unavailable.");

        Assert.False(result.Success);
        Assert.Null(result.Response);
        Assert.Equal(FailureCode.Unavailable, result.Failure?.Code);
    }

    private sealed record TestArguments(string ApplicationName) : IToolArguments;
}
