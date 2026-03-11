using System.Runtime.Versioning;
using Microsoft.Win32;

namespace TimeTracker.Infrastructure;

[SupportedOSPlatform("windows")]
public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ApplicationName = "CopilotTimeTracker";

    public void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                key.SetValue(ApplicationName, $"\"{processPath}\"");
            }
        }
        else
        {
            key.DeleteValue(ApplicationName, throwOnMissingValue: false);
        }
    }
}
