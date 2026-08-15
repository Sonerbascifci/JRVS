using System.Net;
using System.Text;
using System.Text.Json;
using Jarvis.AI.Ollama;
using Jarvis.Core.AI;
using Jarvis.Core.Common;
using Jarvis.Core.Tools;
using Jarvis.Tools.Windows.Applications;
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
        Assert.False(root.TryGetProperty("tools", out _));
        Assert.Equal(
            ["system", "user", "assistant", "user"],
            root.GetProperty("messages")
                .EnumerateArray()
                .Select(message => message.GetProperty("role").GetString()!)
                .ToArray());
    }

    [Fact]
    public async Task GenerateAsync_WithToolHistory_MapsDefinitionsCallsAndResultsWithoutSecurityMetadata()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(
                HttpStatusCode.OK,
                "{\"message\":{\"role\":\"assistant\",\"content\":\"The application is open.\"}}");
        });
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var definition = OpenApplicationDefinition(ToolRiskLevel.Critical);
        var toolCall = new ToolCallRequest(
            "call-1",
            definition.Name,
            new OpenApplicationArguments("notepad"));
        var request = new LlmRequest(
            [
                new(ConversationRole.User, "Open Notepad."),
                new(ConversationRole.Assistant, content: null, [toolCall])
            ],
            [
                new ToolCallResult(
                    "call-1",
                    definition.Name,
                    ToolExecutionResult.Succeeded(
                        new OpenApplicationResultData("notepad", "Notepad")))
            ],
            [definition]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(requestBody);
        Assert.Equal(1, handler.CallCount);

        using var json = JsonDocument.Parse(requestBody);
        var root = json.RootElement;
        var toolDefinition = Assert.Single(root.GetProperty("tools").EnumerateArray());
        Assert.Equal("function", toolDefinition.GetProperty("type").GetString());
        var function = toolDefinition.GetProperty("function");
        Assert.Equal(definition.Name, function.GetProperty("name").GetString());
        Assert.Equal(definition.Description, function.GetProperty("description").GetString());
        var parameters = function.GetProperty("parameters");
        Assert.True(parameters.GetProperty("properties").TryGetProperty("applicationId", out _));
        Assert.Contains(
            "applicationId",
            parameters.GetProperty("required")
                .EnumerateArray()
                .Select(item => item.GetString()));

        var messages = root.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal(["user", "assistant", "tool"], messages.Select(Role).ToArray());
        var historicalCall = Assert.Single(messages[1].GetProperty("tool_calls").EnumerateArray());
        Assert.Equal(
            "notepad",
            historicalCall.GetProperty("function")
                .GetProperty("arguments")
                .GetProperty("applicationId")
                .GetString());
        Assert.Equal(definition.Name, messages[2].GetProperty("tool_name").GetString());
        using var toolContent = JsonDocument.Parse(messages[2].GetProperty("content").GetString()!);
        Assert.True(toolContent.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(
            "Notepad",
            toolContent.RootElement.GetProperty("data").GetProperty("displayName").GetString());

        Assert.DoesNotContain("riskLevel", requestBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("permission", requestBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("confirmation", requestBody, StringComparison.OrdinalIgnoreCase);
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
    public async Task GenerateAsync_WhenResponseContainsToolCall_ReturnsTypedArgumentsAndText()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    """
                    {
                      "message": {
                        "role": "assistant",
                        "content": "Opening Notepad.",
                        "tool_calls": [
                          {
                            "id": "call-7",
                            "type": "function",
                            "function": {
                              "name": "open_application",
                              "arguments": { "applicationId": "notepad" }
                            }
                          }
                        ]
                      }
                    }
                    """)));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = new LlmRequest(
            [new(ConversationRole.User, "Open Notepad.")],
            availableTools: [OpenApplicationDefinition()]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Opening Notepad.", result.Response?.Content);
        var toolCall = Assert.Single(result.Response!.ToolCalls);
        Assert.Equal("call-7", toolCall.CallId);
        Assert.Equal("open_application", toolCall.ToolName);
        var arguments = Assert.IsType<OpenApplicationArguments>(toolCall.Arguments);
        Assert.Equal("notepad", arguments.ApplicationId);
    }

    [Fact]
    public async Task GenerateAsync_WhenResponseContainsMultipleToolCalls_PreservesOrder()
    {
        var handler = new StubHttpMessageHandler(
            (_, _) => Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    """
                    {
                      "message": {
                        "role": "assistant",
                        "content": "Opening both.",
                        "tool_calls": [
                          {
                            "function": {
                              "name": "open_application",
                              "arguments": { "applicationId": "notepad" }
                            }
                          },
                          {
                            "function": {
                              "name": "open_application",
                              "arguments": { "applicationId": "calculator" }
                            }
                          }
                        ]
                      }
                    }
                    """)));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = new LlmRequest(
            [new(ConversationRole.User, "Open both.")],
            availableTools: [OpenApplicationDefinition()]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Opening both.", result.Response?.Content);
        Assert.Equal(["ollama-call-1", "ollama-call-2"], result.Response!.ToolCalls.Select(call => call.CallId));
        Assert.Equal(
            ["notepad", "calculator"],
            result.Response.ToolCalls
                .Select(call => Assert.IsType<OpenApplicationArguments>(call.Arguments).ApplicationId));
    }

    [Fact]
    public async Task GenerateAsync_WhenResponseNamesUnavailableTool_ReturnsUnsupported()
    {
        var handler = ToolCallHandler("unknown_tool", "{}");
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = new LlmRequest(
            [new(ConversationRole.User, "Do it.")],
            availableTools: [OpenApplicationDefinition()]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        AssertFailure(result, FailureCode.Unsupported);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"applicationId\":42}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"ApplicationId\":\"notepad\"}")]
    [InlineData("{\"applicationId\":null}")]
    [InlineData("{\"applicationId\":\"notepad\",\"unexpected\":true}")]
    public async Task GenerateAsync_WhenToolArgumentsAreInvalid_ReturnsInvalidArguments(
        string argumentsJson)
    {
        var handler = ToolCallHandler("open_application", argumentsJson);
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = new LlmRequest(
            [new(ConversationRole.User, "Open Notepad.")],
            availableTools: [OpenApplicationDefinition()]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        AssertFailure(result, FailureCode.InvalidArguments);
    }

    [Fact]
    public async Task GenerateAsync_WhenToolArgumentsPropertyIsMissing_ReturnsInvalidArguments()
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
                        "tool_calls": [
                          { "function": { "name": "open_application" } }
                        ]
                      }
                    }
                    """)));
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var request = new LlmRequest(
            [new(ConversationRole.User, "Open Notepad.")],
            availableTools: [OpenApplicationDefinition()]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        AssertFailure(result, FailureCode.InvalidArguments);
    }

    [Fact]
    public async Task GenerateAsync_WithFailedToolResult_DoesNotExposeInternalFailureDetails()
    {
        string? requestBody = null;
        var handler = new StubHttpMessageHandler(async (request, cancellationToken) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return JsonResponse(
                HttpStatusCode.OK,
                "{\"message\":{\"role\":\"assistant\",\"content\":\"Unable to complete it.\"}}");
        });
        using var client = CreateClient(handler);
        var provider = CreateProvider(client);
        var toolCall = new ToolCallRequest(
            "call-9",
            "open_application",
            new OpenApplicationArguments("notepad"));
        var request = new LlmRequest(
            [new(ConversationRole.Assistant, content: null, [toolCall])],
            [
                new ToolCallResult(
                    "call-9",
                    "open_application",
                    ToolExecutionResult.Failed(
                        FailureCode.PermissionDenied,
                        "Internal policy detail must remain private.",
                        "The action was denied."))
            ]);

        var result = await provider.GenerateAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(requestBody);
        using var json = JsonDocument.Parse(requestBody);
        var toolMessage = json.RootElement.GetProperty("messages").EnumerateArray().Last();
        using var content = JsonDocument.Parse(toolMessage.GetProperty("content").GetString()!);
        Assert.False(content.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("The action was denied.", content.RootElement.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, content.RootElement.GetProperty("data").ValueKind);
        Assert.DoesNotContain("PermissionDenied", requestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Internal policy detail", requestBody, StringComparison.Ordinal);
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

    private static LlmToolDefinition OpenApplicationDefinition(
        ToolRiskLevel riskLevel = ToolRiskLevel.Safe) =>
        LlmToolDefinition.FromDescriptor(
            new ToolDescriptor(
                "open_application",
                "Opens a configured Windows application.",
                riskLevel,
                typeof(OpenApplicationArguments)));

    private static StubHttpMessageHandler ToolCallHandler(
        string toolName,
        string argumentsJson) =>
        new((_, _) => Task.FromResult(
            JsonResponse(
                HttpStatusCode.OK,
                $$"""
                {
                  "message": {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                      {
                        "function": {
                          "name": "{{toolName}}",
                          "arguments": {{argumentsJson}}
                        }
                      }
                    ]
                  }
                }
                """)));

    private static string Role(JsonElement message) =>
        message.GetProperty("role").GetString()!;

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
        Assert.True(
            result.Failure?.Code == expectedCode,
            $"Expected {expectedCode}, received {result.Failure?.Code}: {result.Failure?.Message}");
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
