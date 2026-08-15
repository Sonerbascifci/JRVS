using System.Net;
using System.Text;
using System.Text.Json;
using Jarvis.AI.Ollama;
using Jarvis.Core.AI;
using Jarvis.Core.Common;
using Jarvis.Core.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Jarvis.IntegrationTests;

public sealed class OllamaLlmProviderTests
{
    [Fact]
    public async Task GenerateAsync_WhenResponseIsSuccessful_MapsRequestAndResponse()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/chat", request.RequestUri?.AbsolutePath);
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(
                HttpStatusCode.OK,
                """
                {
                  "model": "test-model:latest",
                  "message": {
                    "role": "assistant",
                    "content": "Merhaba!"
                  },
                  "done": true
                }
                """);
        });
        using var client = CreateClient(handler);
        var provider = CreateProvider(client, "test-model:latest");
        var request = new LlmRequest(
        [
            new(ConversationRole.System, "Be concise."),
            new(ConversationRole.User, "Hello."),
            new(ConversationRole.Assistant, "Previous answer."),
            new(ConversationRole.User, "Continue.")
        ]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Merhaba!", result.Response?.Content);
        Assert.Null(result.Failure);
        Assert.NotNull(requestBody);

        using var json = JsonDocument.Parse(requestBody);
        var root = json.RootElement;
        Assert.Equal("test-model:latest", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(
            ["system", "user", "assistant", "user"],
            root.GetProperty("messages")
                .EnumerateArray()
                .Select(message => message.GetProperty("role").GetString()!)
                .ToArray());
    }

    [Fact]
    public async Task GenerateAsync_WhenRequestContainsToolResults_ReturnsUnsupportedWithoutCallingOllama()
    {
        var handler = SuccessHandler();
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = new LlmRequest(
            [new(ConversationRole.User, "Use a tool.")],
            [new ToolCallResult("call-1", ToolExecutionResult.Succeeded())]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        AssertFailure(result, FailureCode.Unsupported);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_WhenRequestContainsToolMessage_ReturnsUnsupportedWithoutCallingOllama()
    {
        var handler = SuccessHandler();
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = new LlmRequest([new(ConversationRole.Tool, "Tool output.")]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        AssertFailure(result, FailureCode.Unsupported);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GenerateAsync_WhenConnectionFails_ReturnsUnavailable()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("Connection refused.")));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GenerateAsync(UserRequest(), CancellationToken.None);

        AssertFailure(result, FailureCode.Unavailable);
    }

    [Fact]
    public async Task GenerateAsync_WhenConfiguredTimeoutExpires_ReturnsTimeout()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = CreateClient(handler);
        var provider = CreateProvider(client, timeoutSeconds: 1);

        var result = await provider.GenerateAsync(UserRequest(), CancellationToken.None);

        AssertFailure(result, FailureCode.Timeout);
    }

    [Fact]
    public async Task GenerateAsync_WhenCallerCancelsInFlight_ReturnsCancelledInsteadOfTimeout()
    {
        var handler = new StubHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.CancelAfter(TimeSpan.FromMilliseconds(20));

        var result = await provider.GenerateAsync(UserRequest(), cancellationSource.Token);

        AssertFailure(result, FailureCode.Cancelled);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, FailureCode.InvalidArguments)]
    [InlineData(HttpStatusCode.InternalServerError, FailureCode.ExecutionFailed)]
    [InlineData(HttpStatusCode.ServiceUnavailable, FailureCode.Unavailable)]
    public async Task GenerateAsync_WhenOllamaReturnsErrorStatus_MapsFailure(
        HttpStatusCode statusCode,
        FailureCode expectedFailure)
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(statusCode, "{\"error\":\"provider error\"}")));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GenerateAsync(UserRequest(), CancellationToken.None);

        AssertFailure(result, expectedFailure);
    }

    [Fact]
    public async Task GenerateAsync_WhenModelIsMissing_ReturnsNotFoundWithConfiguredModel()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                JsonResponse(HttpStatusCode.NotFound, "{\"error\":\"model not found\"}")));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client, "missing-model:latest");

        var result = await provider.GenerateAsync(UserRequest(), CancellationToken.None);

        AssertFailure(result, FailureCode.NotFound);
        Assert.Contains("missing-model:latest", result.Failure?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_WhenJsonIsMalformed_ReturnsExecutionFailed()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(JsonResponse(HttpStatusCode.OK, "{not-json")));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GenerateAsync(UserRequest(), CancellationToken.None);

        AssertFailure(result, FailureCode.ExecutionFailed);
    }

    [Fact]
    public async Task GenerateAsync_WhenAssistantContentIsEmpty_ReturnsExecutionFailed()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    "{\"message\":{\"role\":\"assistant\",\"content\":\"   \"}}")));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GenerateAsync(UserRequest(), CancellationToken.None);

        AssertFailure(result, FailureCode.ExecutionFailed);
    }

    [Fact]
    public async Task GenerateAsync_WhenSuccessfulPayloadContainsError_ReturnsExecutionFailed()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                JsonResponse(HttpStatusCode.OK, "{\"error\":\"generation failed\"}")));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GenerateAsync(UserRequest(), CancellationToken.None);

        AssertFailure(result, FailureCode.ExecutionFailed);
    }

    [Fact]
    public async Task GenerateAsync_WhenResponseContainsToolCalls_ReturnsUnsupported()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    """
                    {
                      "message": {
                        "role": "assistant",
                        "content": "",
                        "tool_calls": [{ "function": { "name": "get_status", "arguments": {} } }]
                      }
                    }
                    """)));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.GenerateAsync(UserRequest(), CancellationToken.None);

        AssertFailure(result, FailureCode.Unsupported);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_WhenConfiguredModelIsInstalled_ReturnsAvailable()
    {
        var handler = new StubHttpMessageHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("/api/tags", request.RequestUri?.AbsolutePath);
            return Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    "{\"models\":[{\"name\":\"test-model:latest\",\"model\":\"test-model:latest\"}]}"));
        });
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);

        var result = await provider.CheckAvailabilityAsync(CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task CheckAvailabilityAsync_WhenConfiguredModelIsMissing_ReturnsNotFound()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    "{\"models\":[{\"name\":\"another-model:latest\"}]}")));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client, "missing-model:latest");

        var result = await provider.CheckAvailabilityAsync(CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Equal(FailureCode.NotFound, result.Failure?.Code);
        Assert.Contains("missing-model:latest", result.Failure?.Message, StringComparison.Ordinal);
    }

    private static LlmRequest UserRequest() =>
        new([new ConversationMessage(ConversationRole.User, "Hello.")]);

    private static StubHttpMessageHandler SuccessHandler() =>
        new((_, _) => Task.FromResult(
            JsonResponse(
                HttpStatusCode.OK,
                "{\"message\":{\"role\":\"assistant\",\"content\":\"Hello.\"}}")));

    private static HttpClient CreateClient(HttpMessageHandler handler) =>
        new(handler)
        {
            BaseAddress = new Uri("http://localhost:11434/", UriKind.Absolute),
            Timeout = Timeout.InfiniteTimeSpan
        };

    private static OllamaLlmProvider CreateProvider(
        HttpClient client,
        string model = "test-model:latest",
        int timeoutSeconds = 10) =>
        new(
            client,
            Options.Create(
                new OllamaOptions
                {
                    BaseUrl = new Uri("http://localhost:11434", UriKind.Absolute),
                    Model = model,
                    TimeoutSeconds = timeoutSeconds
                }),
            NullLogger<OllamaLlmProvider>.Instance);

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private static void AssertFailure(LlmProviderResult result, FailureCode expectedCode)
    {
        Assert.False(result.Success);
        Assert.Null(result.Response);
        Assert.Equal(expectedCode, result.Failure?.Code);
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return handler(request, cancellationToken);
        }
    }
}
