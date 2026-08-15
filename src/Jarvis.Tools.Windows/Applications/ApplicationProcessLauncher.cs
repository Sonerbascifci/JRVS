using System.Diagnostics;

namespace Jarvis.Tools.Windows.Applications;

public interface IApplicationProcessLauncher
{
    void Launch(string executable);
}

public sealed class WindowsApplicationProcessLauncher : IApplicationProcessLauncher
{
    public void Launch(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Windows did not report a started process.");
    }
}
