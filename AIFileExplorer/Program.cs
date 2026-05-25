using Avalonia;
using System;
using System.IO;

namespace AIFileExplorer;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        LoadEnvFile();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

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

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
