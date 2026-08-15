using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Jarvis.AI.Ollama;

internal sealed record OllamaChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OllamaRequestMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("tools")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<OllamaToolDefinition>? Tools);

internal sealed record OllamaRequestMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("tool_calls")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<OllamaToolCall>? ToolCalls = null,
    [property: JsonPropertyName("tool_name")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ToolName = null);

internal sealed record OllamaToolDefinition(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("function")] OllamaToolFunctionDefinition Function);

internal sealed record OllamaToolFunctionDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("parameters")] JsonNode Parameters);

internal sealed record OllamaToolCall
{
    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }

    [JsonPropertyName("function")]
    public OllamaToolCallFunction? Function { get; init; }
}

internal sealed record OllamaToolCallFunction
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("arguments")]
    public JsonElement Arguments { get; init; }
}

internal sealed record OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaResponseMessage? Message { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }
}

internal sealed record OllamaResponseMessage
{
    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("tool_calls")]
    public OllamaToolCall?[]? ToolCalls { get; init; }
}

internal sealed record OllamaTagsResponse
{
    [JsonPropertyName("models")]
    public OllamaModelInfo[]? Models { get; init; }
}

internal sealed record OllamaModelInfo
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }
}
