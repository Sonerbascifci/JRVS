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
    public ConversationMessage(
        ConversationRole role,
        string? content,
        IEnumerable<ToolCallRequest>? toolCalls = null)
    {
        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown conversation role.");
        }

        if (role == ConversationRole.Tool)
        {
            throw new ArgumentException(
                "Tool output must be supplied through a bound ToolCallResult.",
                nameof(role));
        }

        var toolCallArray = toolCalls?.ToArray() ?? [];
        if (toolCallArray.Any(toolCall => toolCall is null))
        {
            throw new ArgumentException("Tool-call history cannot contain null entries.", nameof(toolCalls));
        }

        if (role != ConversationRole.Assistant && toolCallArray.Length > 0)
        {
            throw new ArgumentException(
                "Only assistant messages can contain tool calls.",
                nameof(toolCalls));
        }

        Content = string.IsNullOrWhiteSpace(content) ? null : content;
        if (Content is null && toolCallArray.Length == 0)
        {
            throw new ArgumentException(
                "A conversation message must contain content or assistant tool calls.",
                nameof(content));
        }

        Role = role;
        ToolCalls = Array.AsReadOnly(toolCallArray);
    }

    public ConversationRole Role { get; }

    public string? Content { get; }

    public IReadOnlyList<ToolCallRequest> ToolCalls { get; }
}

public sealed record LlmToolDefinition
{
    private LlmToolDefinition(string name, string description, Type argumentsType)
    {
        Name = name;
        Description = description;
        ArgumentsType = argumentsType;
    }

    public string Name { get; }

    public string Description { get; }

    public Type ArgumentsType { get; }

    public static LlmToolDefinition FromDescriptor(ToolDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        return new(descriptor.Name, descriptor.Description, descriptor.ArgumentsType);
    }
}

public sealed record LlmRequest
{
    public LlmRequest(
        IEnumerable<ConversationMessage> messages,
        IEnumerable<ToolCallResult>? toolResults = null,
        IEnumerable<LlmToolDefinition>? availableTools = null)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var messageArray = messages.ToArray();
        if (messageArray.Length == 0)
        {
            throw new ArgumentException("At least one conversation message is required.", nameof(messages));
        }

        if (messageArray.Any(message => message is null))
        {
            throw new ArgumentException("Conversation history cannot contain null entries.", nameof(messages));
        }

        var toolResultArray = toolResults?.ToArray() ?? [];
        if (toolResultArray.Any(toolResult => toolResult is null))
        {
            throw new ArgumentException("Tool results cannot contain null entries.", nameof(toolResults));
        }

        var availableToolArray = availableTools?.ToArray() ?? [];
        if (availableToolArray.Any(tool => tool is null))
        {
            throw new ArgumentException("Available tools cannot contain null entries.", nameof(availableTools));
        }

        EnsureUniqueAvailableTools(availableToolArray);
        EnsureToolResultsMatchHistory(messageArray, toolResultArray);

        Messages = Array.AsReadOnly(messageArray);
        ToolResults = Array.AsReadOnly(toolResultArray);
        AvailableTools = Array.AsReadOnly(availableToolArray);
    }

    public IReadOnlyList<ConversationMessage> Messages { get; }

    public IReadOnlyList<ToolCallResult> ToolResults { get; }

    public IReadOnlyList<LlmToolDefinition> AvailableTools { get; }

    private static void EnsureUniqueAvailableTools(IEnumerable<LlmToolDefinition> availableTools)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in availableTools)
        {
            if (!names.Add(tool.Name))
            {
                throw new ArgumentException(
                    $"An LLM tool named '{tool.Name}' is available more than once.",
                    nameof(availableTools));
            }
        }
    }

    private static void EnsureToolResultsMatchHistory(
        IEnumerable<ConversationMessage> messages,
        IEnumerable<ToolCallResult> toolResults)
    {
        var callsById = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var toolCall in messages.SelectMany(message => message.ToolCalls))
        {
            if (!callsById.TryAdd(toolCall.CallId, toolCall.ToolName))
            {
                throw new ArgumentException(
                    $"Assistant tool-call id '{toolCall.CallId}' appears more than once.",
                    nameof(messages));
            }
        }

        var resultIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var toolResult in toolResults)
        {
            if (!resultIds.Add(toolResult.CallId)
                || !callsById.TryGetValue(toolResult.CallId, out var expectedToolName)
                || !string.Equals(expectedToolName, toolResult.ToolName, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Every tool result must match one assistant tool call by id and tool name.",
                    nameof(toolResults));
            }
        }
    }
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
    public ToolCallResult(string callId, string toolName, ToolExecutionResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(callId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(result);

        CallId = callId;
        ToolName = toolName;
        Result = result;
    }

    public string CallId { get; }

    public string ToolName { get; }

    public ToolExecutionResult Result { get; }
}
