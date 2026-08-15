using Jarvis.AI.Ollama;
using Jarvis.Core.AI;
using Jarvis.Core.Tools;
using Jarvis.Desktop;
using Jarvis.Desktop.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Jarvis.IntegrationTests;

public sealed class DesktopCompositionTests
{
    [Fact]
    public async Task BuildHost_WhenStarted_ResolvesFoundationServices()
    {
        using var host = App.BuildHost(["--Jarvis:ApplicationName=JARVIS Test"]);

        await host.StartAsync();

        try
        {
            var options = host.Services.GetRequiredService<IOptions<JarvisOptions>>().Value;
            var registrationProbe = host.Services.GetRequiredService<IServiceProviderIsService>();

            Assert.Equal("JARVIS Test", options.ApplicationName);
            Assert.True(registrationProbe.IsService(typeof(MainWindow)));
            Assert.IsType<OllamaLlmProvider>(host.Services.GetRequiredService<ILlmProvider>());
            Assert.Empty(host.Services.GetRequiredService<IToolRegistry>().Descriptors);
            Assert.NotNull(host.Services.GetService<ILoggerFactory>());
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task StartAsync_WhenApplicationNameIsEmpty_RejectsConfiguration()
    {
        using var host = App.BuildHost(["--Jarvis:ApplicationName="]);

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task BuildHost_BindsAndNormalizesOllamaConfiguration()
    {
        using var host = App.BuildHost(
        [
            "--Jarvis:Llm:BaseUrl=http://127.0.0.1:11434",
            "--Jarvis:Llm:Model=test-model:latest",
            "--Jarvis:Llm:TimeoutSeconds=45"
        ]);

        await host.StartAsync();

        try
        {
            var options = host.Services.GetRequiredService<IOptions<OllamaOptions>>().Value;

            Assert.Equal("http://127.0.0.1:11434/", options.GetNormalizedBaseUrl().AbsoluteUri);
            Assert.Equal("test-model:latest", options.Model);
            Assert.Equal(45, options.TimeoutSeconds);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Theory]
    [InlineData("--Jarvis:Llm:BaseUrl=https://example.com")]
    [InlineData("--Jarvis:Llm:BaseUrl=http://localhost:11434/api")]
    [InlineData("--Jarvis:Llm:BaseUrl=http://user:password@localhost:11434")]
    [InlineData("--Jarvis:Llm:BaseUrl=http://localhost:11434/?source=test")]
    [InlineData("--Jarvis:Llm:Model=")]
    [InlineData("--Jarvis:Llm:TimeoutSeconds=0")]
    public async Task StartAsync_WhenOllamaConfigurationIsInvalid_RejectsConfiguration(string argument)
    {
        using var host = App.BuildHost([argument]);

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public void ServiceCollection_WhenFakeToolIsExplicitlyRegistered_ResolvesItFromRegistry()
    {
        var tool = new FakeTool();
        var services = new ServiceCollection();
        services.AddSingleton<IJarvisTool>(tool);
        services.AddTransient<IToolRegistry, ToolRegistry>();
        using var serviceProvider = services.BuildServiceProvider();

        var result = serviceProvider
            .GetRequiredService<IToolRegistry>()
            .Resolve(tool.Descriptor.Name);

        Assert.True(result.Success);
        Assert.Same(tool, result.Tool);
    }

    private sealed class FakeTool : IJarvisTool
    {
        public ToolDescriptor Descriptor { get; } = new(
            "fake_tool",
            "Fake tool used only for DI composition testing.",
            ToolRiskLevel.Safe,
            typeof(FakeArguments));

        public Task<ToolExecutionResult> ExecuteAsync(
            ToolExecutionContext context,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Composition tests must not execute tools.");
    }

    private sealed record FakeArguments(string Value) : IToolArguments;
}
