using Jarvis.Core.Common;

namespace Jarvis.Core.Tools;

public interface IToolRegistry
{
    IReadOnlyList<ToolDescriptor> Descriptors { get; }

    ToolResolutionResult Resolve(string? toolName);
}

public sealed record ToolResolutionResult
{
    private ToolResolutionResult(IJarvisTool? tool, Failure? failure)
    {
        Tool = tool;
        Failure = failure;
    }

    public bool Success => Tool is not null;

    public IJarvisTool? Tool { get; }

    public Failure? Failure { get; }

    public static ToolResolutionResult Succeeded(IJarvisTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return new(tool, failure: null);
    }

    public static ToolResolutionResult Failed(FailureCode code, string message) =>
        new(tool: null, new Failure(code, message));
}

public sealed class ToolRegistry : IToolRegistry
{
    private readonly Dictionary<string, IJarvisTool> _tools;

    public ToolRegistry(IEnumerable<IJarvisTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        _tools = new(StringComparer.Ordinal);
        var descriptors = new List<ToolDescriptor>();

        foreach (var tool in tools)
        {
            if (tool is null)
            {
                throw new InvalidOperationException("The registered tool collection contains a null tool.");
            }

            var descriptor = tool.Descriptor
                ?? throw new InvalidOperationException("A registered tool returned a null descriptor.");

            if (!_tools.TryAdd(descriptor.Name, tool))
            {
                throw new InvalidOperationException(
                    $"A tool named '{descriptor.Name}' is already registered.");
            }

            descriptors.Add(descriptor);
        }

        Descriptors = Array.AsReadOnly(descriptors.ToArray());
    }

    public IReadOnlyList<ToolDescriptor> Descriptors { get; }

    public ToolResolutionResult Resolve(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return ToolResolutionResult.Failed(
                FailureCode.InvalidArguments,
                "A tool name is required.");
        }

        return _tools.TryGetValue(toolName, out var tool)
            ? ToolResolutionResult.Succeeded(tool)
            : ToolResolutionResult.Failed(
                FailureCode.NotFound,
                "No registered tool was found with the requested name.");
    }
}
