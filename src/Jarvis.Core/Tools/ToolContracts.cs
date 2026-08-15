using Jarvis.Core.Common;

namespace Jarvis.Core.Tools;

public interface IToolArguments;

public interface IToolResultData;

public enum ToolRiskLevel
{
    Safe,
    Confirm,
    Critical
}

public sealed record ToolDescriptor
{
    public ToolDescriptor(
        string name,
        string description,
        ToolRiskLevel riskLevel,
        Type argumentsType)
    {
        if (!IsValidMachineName(name))
        {
            throw new ArgumentException(
                "Tool name must use lowercase snake_case without leading, trailing, or repeated underscores.",
                nameof(name));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        if (!Enum.IsDefined(riskLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(riskLevel), riskLevel, "Unknown tool risk level.");
        }

        ArgumentNullException.ThrowIfNull(argumentsType);
        if (!typeof(IToolArguments).IsAssignableFrom(argumentsType)
            || argumentsType.IsInterface
            || argumentsType.IsAbstract)
        {
            throw new ArgumentException(
                $"Tool arguments type must be a concrete {nameof(IToolArguments)} implementation.",
                nameof(argumentsType));
        }

        Name = name;
        Description = description;
        RiskLevel = riskLevel;
        ArgumentsType = argumentsType;
    }

    public string Name { get; }

    public string Description { get; }

    public ToolRiskLevel RiskLevel { get; }

    public Type ArgumentsType { get; }

    private static bool IsValidMachineName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)
            || !IsLowercaseLetter(name[0])
            || name[^1] == '_')
        {
            return false;
        }

        var previousWasUnderscore = false;
        foreach (var character in name)
        {
            if (character == '_')
            {
                if (previousWasUnderscore)
                {
                    return false;
                }

                previousWasUnderscore = true;
                continue;
            }

            if (!IsLowercaseLetter(character) && !char.IsAsciiDigit(character))
            {
                return false;
            }

            previousWasUnderscore = false;
        }

        return true;
    }

    private static bool IsLowercaseLetter(char character) =>
        character is >= 'a' and <= 'z';
}

public sealed record ToolExecutionContext
{
    public ToolExecutionContext(
        string requestId,
        string? sessionId,
        IToolArguments arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (sessionId is not null && string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("Session identifier cannot be empty.", nameof(sessionId));
        }

        ArgumentNullException.ThrowIfNull(arguments);

        RequestId = requestId;
        SessionId = sessionId;
        Arguments = arguments;
    }

    public string RequestId { get; }

    public string? SessionId { get; }

    public IToolArguments Arguments { get; }
}

public interface IJarvisTool
{
    ToolDescriptor Descriptor { get; }

    Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken);
}

public sealed record ToolExecutionResult
{
    private ToolExecutionResult(
        bool success,
        IToolResultData? data,
        string? userMessage,
        Failure? failure)
    {
        Success = success;
        Data = data;
        UserMessage = userMessage;
        Failure = failure;
    }

    public bool Success { get; }

    public IToolResultData? Data { get; }

    public string? UserMessage { get; }

    public Failure? Failure { get; }

    public static ToolExecutionResult Succeeded(
        IToolResultData? data = null,
        string? userMessage = null) =>
        new(
            success: true,
            data,
            ValidateOptionalUserMessage(userMessage),
            failure: null);

    public static ToolExecutionResult Failed(
        FailureCode code,
        string message,
        string? userMessage = null) =>
        new(
            success: false,
            data: null,
            ValidateOptionalUserMessage(userMessage),
            new Failure(code, message));

    private static string? ValidateOptionalUserMessage(string? userMessage)
    {
        if (userMessage is not null && string.IsNullOrWhiteSpace(userMessage))
        {
            throw new ArgumentException("User message cannot be empty.", nameof(userMessage));
        }

        return userMessage;
    }
}
