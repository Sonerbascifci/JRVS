using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Jarvis.Core.AI;
using Jarvis.Core.Common;
using Jarvis.Core.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis.AI.Ollama;

public sealed class OllamaLlmProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions ProviderJsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly JsonSerializerOptions ToolJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static readonly JsonSchemaExporterOptions SchemaOptions = new()
    {
        TreatNullObliviousAsNonNullable = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaLlmProvider> _logger;
    private readonly string _model;
    private readonly TimeSpan _timeout;

    public OllamaLlmProvider(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaLlmProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;
        _model = options.Value.Model.Trim();
        _timeout = TimeSpan.FromSeconds(options.Value.TimeoutSeconds);
    }

    public async Task<LlmProviderResult> GenerateAsync(
        LlmRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (cancellationToken.IsCancellationRequested)
        {
            return LlmProviderResult.Failed(
                FailureCode.Cancelled,
                "The Ollama request was cancelled.");
        }

        var startedAt = Stopwatch.GetTimestamp();

        _logger.LogInformation(
            "OllamaRequestStarted Model={Model} ToolDefinitionCount={ToolDefinitionCount}",
            _model,
            request.AvailableTools.Count);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        try
        {
            var providerRequest = MapRequest(request);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
            {
                Content = JsonContent.Create(providerRequest, options: ProviderJsonOptions)
            };
            using var response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutSource.Token);

            if (!response.IsSuccessStatusCode)
            {
                var failure = MapHttpFailure(response.StatusCode, modelRequest: true);
                LogFailure("OllamaRequestFailed", failure, response.StatusCode, startedAt);
                return LlmProviderResult.Failed(failure.Code, failure.Message);
            }

            var providerResponse = await response.Content.ReadFromJsonAsync<OllamaChatResponse>(
                ProviderJsonOptions,
                timeoutSource.Token);

            if (providerResponse is null)
            {
                return InvalidResponse("Ollama returned an invalid response.", startedAt);
            }

            if (!string.IsNullOrWhiteSpace(providerResponse.Error))
            {
                return InvalidResponse("Ollama failed to generate a response.", startedAt);
            }

            var assistantMessage = providerResponse.Message;
            if (assistantMessage is null
                || !string.Equals(assistantMessage.Role, "assistant", StringComparison.Ordinal))
            {
                return InvalidResponse(
                    "Ollama returned an invalid assistant response.",
                    startedAt);
            }

            var parsedToolCalls = ParseToolCalls(
                assistantMessage.ToolCalls,
                request.AvailableTools);
            if (parsedToolCalls.Failure is not null)
            {
                LogFailure(
                    "OllamaToolCallRejected",
                    parsedToolCalls.Failure,
                    response.StatusCode,
                    startedAt);
                return LlmProviderResult.Failed(
                    parsedToolCalls.Failure.Code,
                    parsedToolCalls.Failure.Message);
            }

            if (string.IsNullOrWhiteSpace(assistantMessage.Content)
                && parsedToolCalls.ToolCalls.Count == 0)
            {
                return InvalidResponse(
                    "Ollama returned an empty assistant response.",
                    startedAt);
            }

            if (parsedToolCalls.ToolCalls.Count > 0)
            {
                _logger.LogInformation(
                    "OllamaToolCallsReceived Count={ToolCallCount}",
                    parsedToolCalls.ToolCalls.Count);
            }

            _logger.LogInformation(
                "OllamaRequestCompleted Model={Model} StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                _model,
                (int)response.StatusCode,
                GetElapsedMilliseconds(startedAt));

            return LlmProviderResult.Succeeded(
                new LlmResponse(assistantMessage.Content, parsedToolCalls.ToolCalls));
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            var failure = new Failure(FailureCode.Cancelled, "The Ollama request was cancelled.");
            LogException("OllamaRequestCancelled", failure, exception, startedAt);
            return LlmProviderResult.Failed(failure.Code, failure.Message);
        }
        catch (OperationCanceledException exception)
        {
            var failure = new Failure(FailureCode.Timeout, "The Ollama request timed out.");
            LogException("OllamaRequestTimedOut", failure, exception, startedAt);
            return LlmProviderResult.Failed(failure.Code, failure.Message);
        }
        catch (HttpRequestException exception)
        {
            var failure = new Failure(
                FailureCode.Unavailable,
                "The local Ollama service is unavailable.");
            LogException("OllamaUnavailable", failure, exception, startedAt);
            return LlmProviderResult.Failed(failure.Code, failure.Message);
        }
        catch (NotSupportedException exception)
        {
            var failure = new Failure(
                FailureCode.Unsupported,
                "An available tool cannot be represented by the Ollama provider.");
            LogException("OllamaToolSchemaUnsupported", failure, exception, startedAt);
            return LlmProviderResult.Failed(failure.Code, failure.Message);
        }
        catch (JsonException exception)
        {
            var failure = new Failure(
                FailureCode.ExecutionFailed,
                "Ollama returned malformed JSON.");
            LogException("OllamaResponseInvalid", failure, exception, startedAt);
            return LlmProviderResult.Failed(failure.Code, failure.Message);
        }
        catch (Exception exception)
        {
            var failure = new Failure(
                FailureCode.ExecutionFailed,
                "The Ollama request failed unexpectedly.");
            LogException("OllamaRequestFailed", failure, exception, startedAt);
            return LlmProviderResult.Failed(failure.Code, failure.Message);
        }
    }

    public async Task<OllamaAvailabilityResult> CheckAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return OllamaAvailabilityResult.Failed(
                FailureCode.Cancelled,
                "The Ollama availability check was cancelled.");
        }

        var startedAt = Stopwatch.GetTimestamp();
        _logger.LogInformation("OllamaAvailabilityCheckStarted Model={Model}", _model);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        try
        {
            using var response = await _httpClient.GetAsync("api/tags", timeoutSource.Token);
            if (!response.IsSuccessStatusCode)
            {
                var failure = MapHttpFailure(response.StatusCode, modelRequest: false);
                LogFailure("OllamaAvailabilityCheckFailed", failure, response.StatusCode, startedAt);
                return OllamaAvailabilityResult.Failed(failure.Code, failure.Message);
            }

            var tags = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>(
                ProviderJsonOptions,
                timeoutSource.Token);
            if (tags?.Models is null)
            {
                var failure = new Failure(
                    FailureCode.ExecutionFailed,
                    "Ollama returned an invalid model list.");
                LogFailure("OllamaResponseInvalid", failure, response.StatusCode, startedAt);
                return OllamaAvailabilityResult.Failed(failure.Code, failure.Message);
            }

            var modelFound = tags.Models.Any(model =>
                string.Equals(model.Name, _model, StringComparison.OrdinalIgnoreCase)
                || string.Equals(model.Model, _model, StringComparison.OrdinalIgnoreCase));
            if (!modelFound)
            {
                var failure = new Failure(
                    FailureCode.NotFound,
                    $"Configured Ollama model '{_model}' is not installed.");
                LogFailure("OllamaModelNotFound", failure, response.StatusCode, startedAt);
                return OllamaAvailabilityResult.Failed(failure.Code, failure.Message);
            }

            _logger.LogInformation(
                "OllamaAvailabilityCheckCompleted Model={Model} ElapsedMs={ElapsedMs}",
                _model,
                GetElapsedMilliseconds(startedAt));
            return OllamaAvailabilityResult.Available();
        }
        catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
        {
            var failure = new Failure(
                FailureCode.Cancelled,
                "The Ollama availability check was cancelled.");
            LogException("OllamaAvailabilityCheckCancelled", failure, exception, startedAt);
            return OllamaAvailabilityResult.Failed(failure.Code, failure.Message);
        }
        catch (OperationCanceledException exception)
        {
            var failure = new Failure(
                FailureCode.Timeout,
                "The Ollama availability check timed out.");
            LogException("OllamaRequestTimedOut", failure, exception, startedAt);
            return OllamaAvailabilityResult.Failed(failure.Code, failure.Message);
        }
        catch (HttpRequestException exception)
        {
            var failure = new Failure(
                FailureCode.Unavailable,
                "The local Ollama service is unavailable.");
            LogException("OllamaUnavailable", failure, exception, startedAt);
            return OllamaAvailabilityResult.Failed(failure.Code, failure.Message);
        }
        catch (JsonException exception)
        {
            var failure = new Failure(
                FailureCode.ExecutionFailed,
                "Ollama returned a malformed model list.");
            LogException("OllamaResponseInvalid", failure, exception, startedAt);
            return OllamaAvailabilityResult.Failed(failure.Code, failure.Message);
        }
        catch (Exception exception)
        {
            var failure = new Failure(
                FailureCode.ExecutionFailed,
                "The Ollama availability check failed unexpectedly.");
            LogException("OllamaAvailabilityCheckFailed", failure, exception, startedAt);
            return OllamaAvailabilityResult.Failed(failure.Code, failure.Message);
        }
    }

    private OllamaChatRequest MapRequest(LlmRequest request)
    {
        var messages = request.Messages
            .Select(MapMessage)
            .Concat(request.ToolResults.Select(MapToolResult))
            .ToArray();
        var tools = request.AvailableTools.Count == 0
            ? null
            : request.AvailableTools.Select(MapToolDefinition).ToArray();

        return new OllamaChatRequest(_model, messages, Stream: false, tools);
    }

    private static OllamaRequestMessage MapMessage(ConversationMessage message)
    {
        var toolCalls = message.ToolCalls.Count == 0
            ? null
            : message.ToolCalls.Select(MapHistoricalToolCall).ToArray();

        return new OllamaRequestMessage(
            message.Role switch
            {
                ConversationRole.System => "system",
                ConversationRole.User => "user",
                ConversationRole.Assistant => "assistant",
                _ => throw new InvalidOperationException("Unsupported conversation role.")
            },
            message.Content ?? string.Empty,
            toolCalls);
    }

    private static OllamaRequestMessage MapToolResult(ToolCallResult toolResult) =>
        new(
            "tool",
            SerializeToolResult(toolResult.Result),
            ToolName: toolResult.ToolName);

    private static OllamaToolCall MapHistoricalToolCall(ToolCallRequest toolCall) =>
        new()
        {
            Type = "function",
            Function = new OllamaToolCallFunction
            {
                Name = toolCall.ToolName,
                Arguments = JsonSerializer.SerializeToElement(
                    toolCall.Arguments,
                    toolCall.Arguments.GetType(),
                    ToolJsonOptions)
            }
        };

    private static OllamaToolDefinition MapToolDefinition(LlmToolDefinition tool)
    {
        var schema = JsonSchemaExporter.GetJsonSchemaAsNode(
            ToolJsonOptions,
            tool.ArgumentsType,
            SchemaOptions);
        if (schema is not JsonObject)
        {
            throw new NotSupportedException(
                $"Tool arguments for '{tool.Name}' do not produce an object schema.");
        }

        return new OllamaToolDefinition(
            "function",
            new OllamaToolFunctionDefinition(tool.Name, tool.Description, schema));
    }

    private static string SerializeToolResult(ToolExecutionResult result)
    {
        JsonElement? data = result.Data is null
            ? null
            : JsonSerializer.SerializeToElement(
                result.Data,
                result.Data.GetType(),
                ToolJsonOptions);
        var message = result.UserMessage
            ?? (result.Success ? null : "The tool failed.");

        return JsonSerializer.Serialize(
            new OllamaToolResultContent(result.Success, data, message),
            ToolJsonOptions);
    }

    private static ParsedToolCalls ParseToolCalls(
        IReadOnlyList<OllamaToolCall?>? providerToolCalls,
        IReadOnlyList<LlmToolDefinition> availableTools)
    {
        if (providerToolCalls is null || providerToolCalls.Count == 0)
        {
            return ParsedToolCalls.Succeeded([]);
        }

        var definitionsByName = availableTools.ToDictionary(
            tool => tool.Name,
            StringComparer.Ordinal);
        var calls = new List<ToolCallRequest>(providerToolCalls.Count);
        var callIds = new HashSet<string>(StringComparer.Ordinal);

        for (var index = 0; index < providerToolCalls.Count; index++)
        {
            var providerCall = providerToolCalls[index];
            var function = providerCall?.Function;
            if (providerCall is null
                || (!string.IsNullOrWhiteSpace(providerCall.Type)
                    && !string.Equals(providerCall.Type, "function", StringComparison.Ordinal))
                || function is null
                || string.IsNullOrWhiteSpace(function.Name))
            {
                return ParsedToolCalls.Failed(
                    FailureCode.InvalidArguments,
                    "Ollama returned invalid tool-call arguments.");
            }

            if (!definitionsByName.TryGetValue(function.Name, out var definition))
            {
                return ParsedToolCalls.Failed(
                    FailureCode.Unsupported,
                    "Ollama requested a tool that is not available.");
            }

            if (function.Arguments.ValueKind != JsonValueKind.Object)
            {
                return ParsedToolCalls.Failed(
                    FailureCode.InvalidArguments,
                    "Ollama returned invalid tool-call arguments.");
            }

            IToolArguments? arguments;
            try
            {
                arguments = function.Arguments.Deserialize(
                    definition.ArgumentsType,
                    ToolJsonOptions) as IToolArguments;
            }
            catch (JsonException)
            {
                return ParsedToolCalls.Failed(
                    FailureCode.InvalidArguments,
                    "Ollama returned invalid tool-call arguments.");
            }
            catch (NotSupportedException)
            {
                return ParsedToolCalls.Failed(
                    FailureCode.InvalidArguments,
                    "Ollama returned unsupported tool-call arguments.");
            }

            if (arguments is null)
            {
                return ParsedToolCalls.Failed(
                    FailureCode.InvalidArguments,
                    "Ollama returned invalid tool-call arguments.");
            }

            var callId = string.IsNullOrWhiteSpace(providerCall.Id)
                ? $"ollama-call-{index + 1}"
                : providerCall.Id;
            if (!callIds.Add(callId))
            {
                return ParsedToolCalls.Failed(
                    FailureCode.InvalidArguments,
                    "Ollama returned duplicate tool-call identifiers.");
            }

            calls.Add(new ToolCallRequest(callId, definition.Name, arguments));
        }

        return ParsedToolCalls.Succeeded(calls);
    }

    private Failure MapHttpFailure(HttpStatusCode statusCode, bool modelRequest) =>
        statusCode switch
        {
            HttpStatusCode.BadRequest => new(
                FailureCode.InvalidArguments,
                "Ollama rejected the request as invalid."),
            HttpStatusCode.NotFound when modelRequest => new(
                FailureCode.NotFound,
                $"Configured Ollama model '{_model}' is not installed."),
            HttpStatusCode.NotFound => new(
                FailureCode.NotFound,
                "The Ollama model-list endpoint was not found."),
            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout => new(
                FailureCode.Timeout,
                "The Ollama request timed out."),
            HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway
                or HttpStatusCode.ServiceUnavailable => new(
                    FailureCode.Unavailable,
                    "The local Ollama service is temporarily unavailable."),
            >= HttpStatusCode.InternalServerError => new(
                FailureCode.ExecutionFailed,
                "The local Ollama service failed to process the request."),
            _ => new(
                FailureCode.ExecutionFailed,
                "The Ollama request failed.")
        };

    private LlmProviderResult InvalidResponse(string message, long startedAt)
    {
        var failure = new Failure(FailureCode.ExecutionFailed, message);
        LogFailure("OllamaResponseInvalid", failure, HttpStatusCode.OK, startedAt);
        return LlmProviderResult.Failed(failure.Code, failure.Message);
    }

    private void LogFailure(
        string eventName,
        Failure failure,
        HttpStatusCode statusCode,
        long startedAt) =>
        _logger.LogWarning(
            "{EventName} Model={Model} StatusCode={StatusCode} FailureCode={FailureCode} ElapsedMs={ElapsedMs}",
            eventName,
            _model,
            (int)statusCode,
            failure.Code,
            GetElapsedMilliseconds(startedAt));

    private void LogException(
        string eventName,
        Failure failure,
        Exception exception,
        long startedAt) =>
        _logger.LogWarning(
            exception,
            "{EventName} Model={Model} FailureCode={FailureCode} ElapsedMs={ElapsedMs}",
            eventName,
            _model,
            failure.Code,
            GetElapsedMilliseconds(startedAt));

    private static long GetElapsedMilliseconds(long startedAt) =>
        (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    private sealed record OllamaToolResultContent(
        bool Success,
        JsonElement? Data,
        string? Message);

    private sealed record ParsedToolCalls
    {
        private ParsedToolCalls(
            IReadOnlyList<ToolCallRequest> toolCalls,
            Failure? failure)
        {
            ToolCalls = toolCalls;
            Failure = failure;
        }

        public IReadOnlyList<ToolCallRequest> ToolCalls { get; }

        public Failure? Failure { get; }

        public static ParsedToolCalls Succeeded(IReadOnlyList<ToolCallRequest> toolCalls) =>
            new(toolCalls, failure: null);

        public static ParsedToolCalls Failed(FailureCode code, string message) =>
            new([], new Failure(code, message));
    }
}

public sealed record OllamaAvailabilityResult
{
    private OllamaAvailabilityResult(Failure? failure)
    {
        Failure = failure;
    }

    public bool IsAvailable => Failure is null;

    public Failure? Failure { get; }

    internal static OllamaAvailabilityResult Available() => new(failure: null);

    internal static OllamaAvailabilityResult Failed(FailureCode code, string message) =>
        new(new Failure(code, message));
}
