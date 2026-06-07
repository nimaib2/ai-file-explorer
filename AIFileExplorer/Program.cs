using Avalonia;
using System;
using System.IO;

namespace AIFileExplorer;

/// <summary>
/// Application entry point. Loads the <c>.env</c> file (if present) so the
/// <c>ANTHROPIC_API_KEY</c> environment variable is set before any SDK call,
/// then hands control to Avalonia's desktop lifetime.
/// </summary>
class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        LoadEnvFile();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Reads key=value pairs from a <c>.env</c> file four directories above the
    /// output binary (i.e. the repo root when running under <c>dotnet run</c>)
    /// and injects them into the process environment. Lines starting with <c>#</c>
    /// and blank lines are ignored. Silently no-ops if the file does not exist.
    /// </summary>
    private static void LoadEnvFile()
    {
        var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".env");
        if (!File.Exists(envPath))
            return;

        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;

            var idx = trimmed.IndexOf('=');
            if (idx < 0)
                continue;

            var key = trimmed[..idx].Trim();
            var value = trimmed[(idx + 1)..].Trim();
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// Configures the Avalonia application builder. Kept public so the Avalonia
    /// visual designer can call it without instantiating the full app.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
