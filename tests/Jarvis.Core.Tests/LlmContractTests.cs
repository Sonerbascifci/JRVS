using Jarvis.Core.AI;
using Jarvis.Core.Common;
using Jarvis.Core.Tools;

namespace Jarvis.Core.Tests;

public sealed class LlmContractTests
{
    [Fact]
    public void LlmRequest_CopiesInputCollections()
    {
        var descriptor = new ToolDescriptor(
            "open_application",
            "Opens an application.",
            ToolRiskLevel.Safe,
            typeof(TestArguments));
        var definition = LlmToolDefinition.FromDescriptor(descriptor);
        var toolCall = new ToolCallRequest(
            "call-1",
            descriptor.Name,
            new TestArguments("notepad"));
        var messages = new List<ConversationMessage>
        {
            new(ConversationRole.User, "Open my project."),
            new(ConversationRole.Assistant, content: null, [toolCall])
        };
        var toolResults = new List<ToolCallResult>
        {
            new("call-1", descriptor.Name, ToolExecutionResult.Succeeded())
        };
        var availableTools = new List<LlmToolDefinition> { definition };

        var request = new LlmRequest(messages, toolResults, availableTools);
        messages.Clear();
        toolResults.Clear();
        availableTools.Clear();

        Assert.Equal(2, request.Messages.Count);
        Assert.Single(request.ToolResults);
        Assert.Same(definition, Assert.Single(request.AvailableTools));
    }

    [Fact]
    public void LlmToolDefinition_FromDescriptor_ProjectsOnlyModelVisibleFields()
    {
        var descriptor = new ToolDescriptor(
            "open_application",
            "Opens an application.",
            ToolRiskLevel.Critical,
            typeof(TestArguments));

        var definition = LlmToolDefinition.FromDescriptor(descriptor);

        Assert.Equal(descriptor.Name, definition.Name);
        Assert.Equal(descriptor.Description, definition.Description);
        Assert.Equal(descriptor.ArgumentsType, definition.ArgumentsType);
    }

    [Fact]
    public void LlmRequest_WhenToolResultDoesNotMatchAssistantHistory_Throws()
    {
        var toolCall = new ToolCallRequest(
            "call-1",
            "open_application",
            new TestArguments("notepad"));

        Assert.Throws<ArgumentException>(() => new LlmRequest(
            [new(ConversationRole.Assistant, content: null, [toolCall])],
            [new("call-1", "different_tool", ToolExecutionResult.Succeeded())]));
    }

    [Fact]
    public void ConversationMessage_WhenToolRoleIsCreatedDirectly_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new ConversationMessage(ConversationRole.Tool, "Unbound output."));
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
