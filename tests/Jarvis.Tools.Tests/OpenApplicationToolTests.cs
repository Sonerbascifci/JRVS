using Jarvis.Core.Common;
using Jarvis.Core.Tools;
using Jarvis.Tools.Windows.Applications;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jarvis.Tools.Tests;

public sealed class OpenApplicationToolTests
{
    [Fact]
    public void Descriptor_UsesStableNameTypedArgumentsAndSafeRisk()
    {
        var tool = CreateTool(new FakeLauncher(), Definition());

        Assert.Equal("open_application", tool.Descriptor.Name);
        Assert.Equal(ToolRiskLevel.Safe, tool.Descriptor.RiskLevel);
        Assert.Equal(typeof(OpenApplicationArguments), tool.Descriptor.ArgumentsType);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApplicationIsKnown_LaunchesTrustedExecutableOnce()
    {
        var launcher = new FakeLauncher();
        var tool = CreateTool(launcher, Definition());

        var result = await ExecuteAsync(tool, "notes");

        Assert.True(result.Success);
        Assert.Equal(["notepad.exe"], launcher.Executables);
        var data = Assert.IsType<OpenApplicationResultData>(result.Data);
        Assert.Equal("notes", data.ApplicationId);
        Assert.Equal("Notepad", data.DisplayName);
        Assert.Null(result.UserMessage);
        Assert.Null(result.Failure);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApplicationIsUnknown_ReturnsNotFoundWithoutLaunching()
    {
        var launcher = new FakeLauncher();
        var tool = CreateTool(launcher, Definition());

        var result = await ExecuteAsync(tool, "unknown_app");

        Assert.False(result.Success);
        Assert.Equal(FailureCode.NotFound, result.Failure?.Code);
        Assert.Empty(launcher.Executables);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task ExecuteAsync_WhenApplicationIdIsEmpty_ReturnsInvalidArgumentsWithoutLaunching(
        string? applicationId)
    {
        var launcher = new FakeLauncher();
        var tool = CreateTool(launcher, Definition());

        var result = await ExecuteAsync(tool, applicationId);

        Assert.False(result.Success);
        Assert.Equal(FailureCode.InvalidArguments, result.Failure?.Code);
        Assert.Empty(launcher.Executables);
    }

    [Fact]
    public async Task ExecuteAsync_WhenApplicationIdExceedsLimit_ReturnsInvalidArgumentsWithoutLaunching()
    {
        var launcher = new FakeLauncher();
        var tool = CreateTool(launcher, Definition());
        var applicationId = new string('a', OpenApplicationArguments.MaximumApplicationIdLength + 1);

        var result = await ExecuteAsync(tool, applicationId);

        Assert.False(result.Success);
        Assert.Equal(FailureCode.InvalidArguments, result.Failure?.Code);
        Assert.Empty(launcher.Executables);
    }

    [Theory]
    [InlineData("notepad.exe")]
    [InlineData("C:\\Windows\\System32\\notepad.exe")]
    [InlineData("notepad & calc")]
    public async Task ExecuteAsync_WhenCallerProvidesExecutablePathOrShellText_DoesNotLaunch(
        string untrustedApplicationId)
    {
        var launcher = new FakeLauncher();
        var tool = CreateTool(launcher, Definition());

        var result = await ExecuteAsync(tool, untrustedApplicationId);

        Assert.False(result.Success);
        Assert.Equal(FailureCode.NotFound, result.Failure?.Code);
        Assert.Empty(launcher.Executables);
    }

    [Theory]
    [InlineData("cmd.exe")]
    [InlineData("powershell.exe")]
    [InlineData("pwsh.exe")]
    [InlineData("wscript.exe")]
    [InlineData("cscript.exe")]
    public void Catalog_WhenExecutableIsDangerous_RejectsConfiguration(string executable)
    {
        var definition = Definition(id: "terminal", executable: executable);

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ConfiguredApplicationCatalog([definition]));

        Assert.Equal("Configured application 'terminal' uses a blocked executable.", exception.Message);
    }

    [Fact]
    public void Catalog_WhenApplicationIdIsDuplicated_RejectsConfigurationDeterministically()
    {
        var first = Definition();
        var second = Definition(executable: "other.exe");

        var exception = Assert.Throws<InvalidOperationException>(
            () => new ConfiguredApplicationCatalog([first, second]));

        Assert.Equal("An application named 'notes' is configured more than once.", exception.Message);
    }

    [Fact]
    public void Catalog_WhenExecutableIsEmpty_RejectsConfiguration()
    {
        var definition = Definition(executable: " ");

        Assert.Throws<InvalidOperationException>(
            () => new ConfiguredApplicationCatalog([definition]));
    }

    [Fact]
    public void Catalog_WhenExecutableIsUrl_RejectsConfiguration()
    {
        var definition = Definition(executable: "https://example.com/notepad.exe");

        Assert.Throws<InvalidOperationException>(
            () => new ConfiguredApplicationCatalog([definition]));
    }

    [Fact]
    public void Catalog_WhenApplicationIdIsEmpty_RejectsConfiguration()
    {
        var definition = Definition(id: " ");

        Assert.Throws<InvalidOperationException>(
            () => new ConfiguredApplicationCatalog([definition]));
    }

    [Fact]
    public async Task ExecuteAsync_WhenLauncherFails_ReturnsControlledFailureWithoutRawException()
    {
        var launcher = new FakeLauncher
        {
            ExceptionToThrow = new InvalidOperationException("sensitive raw failure")
        };
        var tool = CreateTool(launcher, Definition());

        var result = await ExecuteAsync(tool, "notes");

        Assert.False(result.Success);
        Assert.Equal(FailureCode.ExecutionFailed, result.Failure?.Code);
        Assert.DoesNotContain("sensitive raw failure", result.Failure?.Message);
        Assert.Equal(["notepad.exe"], launcher.Executables);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadyCancelled_ReturnsCancelledWithoutLaunching()
    {
        var launcher = new FakeLauncher();
        var tool = CreateTool(launcher, Definition());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var result = await ExecuteAsync(tool, "notes", cancellation.Token);

        Assert.False(result.Success);
        Assert.Equal(FailureCode.Cancelled, result.Failure?.Code);
        Assert.Empty(launcher.Executables);
    }

    [Fact]
    public void Contracts_DoNotExposeProcessArguments()
    {
        Assert.Null(typeof(OpenApplicationArguments).GetProperty("Executable"));
        Assert.Null(typeof(OpenApplicationArguments).GetProperty("Arguments"));
        Assert.Null(typeof(ApplicationDefinition).GetProperty("Arguments"));
        Assert.Null(typeof(ApplicationDefinition).GetProperty("ArgumentList"));
        Assert.Null(typeof(ApplicationDefinition).GetProperty("Verb"));
        Assert.Null(typeof(ApplicationDefinition).GetProperty("WorkingDirectory"));
    }

    private static OpenApplicationTool CreateTool(
        FakeLauncher launcher,
        params ApplicationDefinition[] definitions) =>
        new(
            new ConfiguredApplicationCatalog(definitions),
            launcher,
            NullLogger<OpenApplicationTool>.Instance);

    private static ApplicationDefinition Definition(
        string id = "notes",
        string displayName = "Notepad",
        string executable = "notepad.exe") =>
        new()
        {
            Id = id,
            DisplayName = displayName,
            Executable = executable
        };

    private static Task<ToolExecutionResult> ExecuteAsync(
        OpenApplicationTool tool,
        string? applicationId,
        CancellationToken cancellationToken = default) =>
        tool.ExecuteAsync(
            new ToolExecutionContext(
                "request-1",
                "session-1",
                new OpenApplicationArguments(applicationId!)),
            cancellationToken);

    private sealed class FakeLauncher : IApplicationProcessLauncher
    {
        public List<string> Executables { get; } = [];

        public Exception? ExceptionToThrow { get; init; }

        public void Launch(string executable)
        {
            Executables.Add(executable);

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }
        }
    }
}
