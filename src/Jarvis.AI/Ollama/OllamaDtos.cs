using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jarvis.AI.Ollama;

internal sealed record OllamaChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] IReadOnlyList<OllamaRequestMessage> Messages,
    [property: JsonPropertyName("stream")] bool Stream);

internal sealed record OllamaRequestMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

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
    public JsonElement? ToolCalls { get; init; }
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
