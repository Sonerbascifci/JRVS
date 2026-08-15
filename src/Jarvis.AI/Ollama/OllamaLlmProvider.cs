using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Jarvis.Core.AI;
using Jarvis.Core.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis.AI.Ollama;

public sealed class OllamaLlmProvider : ILlmProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

        if (request.ToolResults.Count > 0
            || request.Messages.Any(message => message.Role == ConversationRole.Tool))
        {
            return LlmProviderResult.Failed(
                FailureCode.Unsupported,
                "Tool messages and tool results are not supported by the AI-001 Ollama provider.");
        }

        var providerRequest = new OllamaChatRequest(
            _model,
            request.Messages.Select(MapMessage).ToArray(),
            Stream: false);
        var startedAt = Stopwatch.GetTimestamp();

        _logger.LogInformation(
            "OllamaRequestStarted Model={Model}",
            _model);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/chat")
            {
                Content = JsonContent.Create(providerRequest, options: JsonOptions)
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
                JsonOptions,
                timeoutSource.Token);

            if (providerResponse is null)
            {
                return InvalidResponse("Ollama returned an invalid response.", startedAt);
            }

            if (!string.IsNullOrWhiteSpace(providerResponse.Error))
            {
                return InvalidResponse("Ollama failed to generate a response.", startedAt);
            }

            if (ContainsToolCalls(providerResponse.Message?.ToolCalls))
            {
                var failure = new Failure(
                    FailureCode.Unsupported,
                    "Ollama tool calls are not supported by the AI-001 provider.");
                LogFailure("OllamaResponseUnsupported", failure, response.StatusCode, startedAt);
                return LlmProviderResult.Failed(failure.Code, failure.Message);
            }

            var assistantMessage = providerResponse.Message;
            if (assistantMessage is null
                || !string.Equals(assistantMessage.Role, "assistant", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                return InvalidResponse(
                    "Ollama returned an empty or invalid assistant response.",
                    startedAt);
            }

            _logger.LogInformation(
                "OllamaRequestCompleted Model={Model} StatusCode={StatusCode} ElapsedMs={ElapsedMs}",
                _model,
                (int)response.StatusCode,
                GetElapsedMilliseconds(startedAt));

            return LlmProviderResult.Succeeded(new LlmResponse(assistantMessage.Content));
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
                JsonOptions,
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

    private static OllamaRequestMessage MapMessage(ConversationMessage message) =>
        new(
            message.Role switch
            {
                ConversationRole.System => "system",
                ConversationRole.User => "user",
                ConversationRole.Assistant => "assistant",
                _ => throw new InvalidOperationException("Unsupported conversation role.")
            },
            message.Content);

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

    private static bool ContainsToolCalls(JsonElement? toolCalls)
    {
        if (toolCalls is null
            || toolCalls.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        return toolCalls.Value.ValueKind != JsonValueKind.Array
            || toolCalls.Value.GetArrayLength() > 0;
    }

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
