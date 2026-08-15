namespace Jarvis.Tools.Windows.Applications;

public sealed class WindowsToolsOptions
{
    public const string SectionName = "Jarvis:Windows";

    public List<ApplicationDefinition> Applications { get; init; } = [];
}

public sealed record ApplicationDefinition
{
    public string Id { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Executable { get; init; } = string.Empty;
}

public interface IApplicationCatalog
{
    bool TryGet(string applicationId, out ApplicationDefinition? definition);
}

public sealed class ConfiguredApplicationCatalog : IApplicationCatalog
{
    private static readonly HashSet<string> BlockedExecutables = new(
        ["cmd.exe", "powershell.exe", "pwsh.exe", "wscript.exe", "cscript.exe"],
        StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, ApplicationDefinition> _applications =
        new(StringComparer.Ordinal);

    public ConfiguredApplicationCatalog(IEnumerable<ApplicationDefinition> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);

        foreach (var application in applications)
        {
            if (application is null)
            {
                throw new InvalidOperationException(
                    "The configured application catalog contains a null entry.");
            }

            var definition = ValidateAndCopy(application);
            if (!_applications.TryAdd(definition.Id, definition))
            {
                throw new InvalidOperationException(
                    $"An application named '{definition.Id}' is configured more than once.");
            }
        }
    }

    public bool TryGet(string applicationId, out ApplicationDefinition? definition) =>
        _applications.TryGetValue(applicationId, out definition);

    private static ApplicationDefinition ValidateAndCopy(ApplicationDefinition application)
    {
        if (string.IsNullOrWhiteSpace(application.Id))
        {
            throw new InvalidOperationException("A configured application identifier is required.");
        }

        var id = application.Id.Trim();
        if (id.Length > OpenApplicationArguments.MaximumApplicationIdLength)
        {
            throw new InvalidOperationException(
                $"Configured application identifiers cannot exceed {OpenApplicationArguments.MaximumApplicationIdLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(application.DisplayName))
        {
            throw new InvalidOperationException(
                $"Configured application '{id}' requires a display name.");
        }

        if (string.IsNullOrWhiteSpace(application.Executable))
        {
            throw new InvalidOperationException(
                $"Configured application '{id}' requires an executable.");
        }

        var executable = application.Executable.Trim();
        if (executable.IndexOfAny(['\0', '\r', '\n', '"']) >= 0)
        {
            throw new InvalidOperationException(
                $"Configured application '{id}' has an invalid executable.");
        }

        if (Uri.TryCreate(executable, UriKind.Absolute, out var executableUri)
            && !executableUri.IsFile)
        {
            throw new InvalidOperationException(
                $"Configured application '{id}' must not reference a non-file URI.");
        }

        var executableName = Path.GetFileName(executable);
        if (!executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Configured application '{id}' must reference a Windows executable.");
        }

        if (BlockedExecutables.Contains(executableName))
        {
            throw new InvalidOperationException(
                $"Configured application '{id}' uses a blocked executable.");
        }

        return new ApplicationDefinition
        {
            Id = id,
            DisplayName = application.DisplayName.Trim(),
            Executable = executable
        };
    }
}
