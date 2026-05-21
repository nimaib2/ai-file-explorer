using System.Collections.Generic;

namespace AIFileExplorer.Services;

/// <summary>
/// Maps file extensions to emoji icons for display in the file list.
///
/// Emoji are used instead of Shell32/NSWorkspace icon APIs because:
///   - Shell32 is Windows-only
///   - NSWorkspace requires macOS-specific P/Invoke
///   - Emoji render everywhere .NET runs with zero dependencies
///
/// The dictionary uses OrdinalIgnoreCase so ".PNG" and ".png" both match.
/// Unrecognised extensions fall back to a generic file icon.
/// </summary>
public static class FileTypeIcons
{
    public const string Folder  = "📁";
    public const string Generic = "📄";

    private static readonly Dictionary<string, string> Map =
        new(System.StringComparer.OrdinalIgnoreCase)
        {
            // Images
            { ".png",  "🖼️" }, { ".jpg",  "🖼️" }, { ".jpeg", "🖼️" },
            { ".gif",  "🖼️" }, { ".bmp",  "🖼️" }, { ".svg",  "🖼️" },
            { ".webp", "🖼️" }, { ".ico",  "🖼️" }, { ".tiff", "🖼️" },

            // Documents
            { ".pdf",  "📄" },
            { ".doc",  "📝" }, { ".docx", "📝" },
            { ".xls",  "📊" }, { ".xlsx", "📊" },
            { ".ppt",  "📊" }, { ".pptx", "📊" },

            // Plain text / markup
            { ".txt",  "📃" }, { ".md",   "📃" }, { ".log",  "📃" },
            { ".rtf",  "📃" }, { ".csv",  "📃" },

            // Code
            { ".cs",   "💻" }, { ".js",   "💻" }, { ".ts",   "💻" },
            { ".py",   "💻" }, { ".go",   "💻" }, { ".rs",   "💻" },
            { ".java", "💻" }, { ".cpp",  "💻" }, { ".c",    "💻" },
            { ".h",    "💻" }, { ".rb",   "💻" }, { ".swift","💻" },
            { ".kt",   "💻" }, { ".dart", "💻" }, { ".php",  "💻" },
            { ".lua",  "💻" }, { ".r",    "💻" },

            // Web
            { ".html", "🌐" }, { ".htm",  "🌐" }, { ".css",  "🌐" },

            // Config / data
            { ".json", "⚙️" }, { ".yaml", "⚙️" }, { ".yml",  "⚙️" },
            { ".toml", "⚙️" }, { ".xml",  "⚙️" }, { ".ini",  "⚙️" },
            { ".env",  "⚙️" }, { ".conf", "⚙️" },

            // Shell scripts
            { ".sh",   "🐚" }, { ".bash", "🐚" }, { ".zsh",  "🐚" },
            { ".fish", "🐚" }, { ".ps1",  "🐚" },

            // Audio
            { ".mp3",  "🎵" }, { ".wav",  "🎵" }, { ".flac", "🎵" },
            { ".aac",  "🎵" }, { ".m4a",  "🎵" }, { ".ogg",  "🎵" },

            // Video
            { ".mp4",  "🎬" }, { ".mov",  "🎬" }, { ".avi",  "🎬" },
            { ".mkv",  "🎬" }, { ".webm", "🎬" },

            // Archives
            { ".zip",  "📦" }, { ".tar",  "📦" }, { ".gz",   "📦" },
            { ".7z",   "📦" }, { ".rar",  "📦" }, { ".bz2",  "📦" },

            // Executables / installers
            { ".exe",  "⚡" }, { ".app",  "⚡" }, { ".dmg",  "⚡" },
            { ".deb",  "⚡" }, { ".rpm",  "⚡" },
        };

    public static string ForExtension(string extension) =>
        Map.TryGetValue(extension, out var icon) ? icon : Generic;
}
