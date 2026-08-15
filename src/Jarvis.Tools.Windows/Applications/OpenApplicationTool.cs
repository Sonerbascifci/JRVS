using System.Diagnostics;
using Jarvis.Core.Common;
using Jarvis.Core.Tools;
using Microsoft.Extensions.Logging;

namespace Jarvis.Tools.Windows.Applications;

public sealed record OpenApplicationArguments(string ApplicationId) : IToolArguments
{
    public const int MaximumApplicationIdLength = 64;
}

public sealed record OpenApplicationResultData(
    string ApplicationId,
    string DisplayName) : IToolResultData;

public sealed class OpenApplicationTool : IJarvisTool
{
    private readonly IApplicationCatalog _catalog;
    private readonly IApplicationProcessLauncher _launcher;
    private readonly ILogger<OpenApplicationTool> _logger;

    public OpenApplicationTool(
        IApplicationCatalog catalog,
        IApplicationProcessLauncher launcher,
        ILogger<OpenApplicationTool> logger)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(logger);

        _catalog = catalog;
        _launcher = launcher;
        _logger = logger;
    }

    public ToolDescriptor Descriptor { get; } = new(
        "open_application",
        "Opens an explicitly configured Windows application by its logical identifier.",
        ToolRiskLevel.Safe,
        typeof(OpenApplicationArguments));

    public Task<ToolExecutionResult> ExecuteAsync(
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Cancelled());
        }

        if (context is null || context.Arguments is not OpenApplicationArguments arguments)
        {
            return Task.FromResult(ToolExecutionResult.Failed(
                FailureCode.InvalidArguments,
                "The open_application tool requires typed application arguments."));
        }

        if (string.IsNullOrWhiteSpace(arguments.ApplicationId)
            || arguments.ApplicationId.Length > OpenApplicationArguments.MaximumApplicationIdLength)
        {
            return Task.FromResult(ToolExecutionResult.Failed(
                FailureCode.InvalidArguments,
                $"ApplicationId is required and cannot exceed {OpenApplicationArguments.MaximumApplicationIdLength} characters."));
        }

        if (!_catalog.TryGet(arguments.ApplicationId, out var application)
            || application is null)
        {
            _logger.LogWarning(
                "ToolFailed Tool={ToolName} FailureCategory={FailureCategory}",
                Descriptor.Name,
                "ApplicationNotFound");

            return Task.FromResult(ToolExecutionResult.Failed(
                FailureCode.NotFound,
                "No configured application matches the requested identifier."));
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(Cancelled());
        }

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            _launcher.Launch(application.Executable);

            _logger.LogInformation(
                "ToolCompleted Tool={ToolName} ApplicationId={ApplicationId} Success={Success} DurationMs={DurationMs}",
                Descriptor.Name,
                application.Id,
                true,
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

            return Task.FromResult(ToolExecutionResult.Succeeded(
                new OpenApplicationResultData(application.Id, application.DisplayName)));
        }
        catch (Exception exception)
        {
            _logger.LogError(
                "ToolFailed Tool={ToolName} ApplicationId={ApplicationId} Success={Success} FailureCategory={FailureCategory} FailureType={FailureType} DurationMs={DurationMs}",
                Descriptor.Name,
                application.Id,
                false,
                "ApplicationLaunchFailed",
                exception.GetType().Name,
                Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);

            return Task.FromResult(ToolExecutionResult.Failed(
                FailureCode.ExecutionFailed,
                "The configured application could not be launched."));
        }
    }

    private static ToolExecutionResult Cancelled() =>
        ToolExecutionResult.Failed(
            FailureCode.Cancelled,
            "The application launch was cancelled before execution.");
}
