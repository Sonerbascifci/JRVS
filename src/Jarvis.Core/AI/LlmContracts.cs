using Jarvis.Core.Common;
using Jarvis.Core.Tools;

namespace Jarvis.Core.AI;

public interface ILlmProvider
{
    Task<LlmProviderResult> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken);
}

public enum ConversationRole
{
    System,
    User,
    Assistant,
    Tool
}

public sealed record ConversationMessage
{
    public ConversationMessage(ConversationRole role, string content)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown conversation role.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Role = role;
        Content = content;
    }

    public ConversationRole Role { get; }

    public string Content { get; }
}

public sealed record LlmRequest
{
    public LlmRequest(
        IEnumerable<ConversationMessage> messages,
        IEnumerable<ToolCallResult>? toolResults = null)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageArray = messages.ToArray();
        if (messageArray.Length == 0)
        {
            throw new ArgumentException("At least one conversation message is required.", nameof(messages));
        }

        Messages = Array.AsReadOnly(messageArray);
        ToolResults = Array.AsReadOnly(toolResults?.ToArray() ?? []);
    }

    public IReadOnlyList<ConversationMessage> Messages { get; }

    public IReadOnlyList<ToolCallResult> ToolResults { get; }
}

public sealed record LlmResponse
{
    public LlmResponse(
        string? content,
        IEnumerable<ToolCallRequest>? toolCalls = null)
    {
        var toolCallArray = toolCalls?.ToArray() ?? [];
        Content = string.IsNullOrWhiteSpace(content) ? null : content;

        if (Content is null && toolCallArray.Length == 0)
        {
            throw new ArgumentException("An LLM response must contain content or at least one tool call.");
        }

        ToolCalls = Array.AsReadOnly(toolCallArray);
    }

    public string? Content { get; }

    public IReadOnlyList<ToolCallRequest> ToolCalls { get; }
}

public sealed record LlmProviderResult
{
    private LlmProviderResult(LlmResponse? response, Failure? failure)
    {
        Response = response;
        Failure = failure;
    }

    public bool Success => Response is not null;

    public LlmResponse? Response { get; }

    public Failure? Failure { get; }

    public static LlmProviderResult Succeeded(LlmResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        return new(response, failure: null);
    }

    public static LlmProviderResult Failed(FailureCode code, string message) =>
        new(response: null, new Failure(code, message));
}

public sealed record ToolCallRequest
{
    public ToolCallRequest(string callId, string toolName, IToolArguments arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);

        CallId = callId;
        ToolName = toolName;
        Arguments = arguments;
    }

    public string CallId { get; }

    public string ToolName { get; }

    public IToolArguments Arguments { get; }
}

public sealed record ToolCallResult
{
    public ToolCallResult(string callId, ToolExecutionResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentNullException.ThrowIfNull(result);

        CallId = callId;
        Result = result;
    }

    public string CallId { get; }

    public ToolExecutionResult Result { get; }
}
