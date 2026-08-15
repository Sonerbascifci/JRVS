using System.Windows;
using Jarvis.AI.Ollama;
using Jarvis.Core.AI;
using Jarvis.Core.Tools;
using Jarvis.Desktop.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jarvis.Desktop;

public partial class App : Application
{
    private IHost? _host;

    public static IHost BuildHost(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory
        });

        builder.Services
            .AddOptions<JarvisOptions>()
            .Bind(builder.Configuration.GetSection(JarvisOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.ApplicationName),
                "Jarvis:ApplicationName must not be empty.")
            .ValidateOnStart();

        builder.Services
            .AddOptions<OllamaOptions>()
            .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
            .Validate(
                options => options.HasValidLocalBaseUrl(),
                "Jarvis:Llm:BaseUrl must be an absolute loopback HTTP or HTTPS URL without a path, query, credentials, or fragment.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Model),
                "Jarvis:Llm:Model must not be empty.")
            .Validate(
                options => options.TimeoutSeconds > 0,
                "Jarvis:Llm:TimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        builder.Services.AddHttpClient<OllamaLlmProvider>((services, client) =>
        {
            var options = services.GetRequiredService<Microsoft.Extensions.Options.IOptions<OllamaOptions>>().Value;
            client.BaseAddress = options.GetNormalizedBaseUrl();
            client.Timeout = Timeout.InfiniteTimeSpan;
        });
        builder.Services.AddTransient<ILlmProvider>(
            services => services.GetRequiredService<OllamaLlmProvider>());
        builder.Services.AddTransient<IToolRegistry, ToolRegistry>();

        builder.Services.AddSingleton<MainWindow>();

        return builder.Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = BuildHost(e.Args);
        await _host.StartAsync();

        _host.Services
            .GetRequiredService<ILogger<App>>()
            .LogInformation("JARVIS desktop host started");

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services
                .GetRequiredService<ILogger<App>>()
                .LogInformation("JARVIS desktop host stopping");

            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
