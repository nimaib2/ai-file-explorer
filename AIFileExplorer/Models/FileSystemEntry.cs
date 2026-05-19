using System;

namespace AIFileExplorer.Models;

/// <summary>
/// Represents a single file or directory in a listed directory.
/// Plain data class — no Avalonia or UI dependencies.
/// </summary>
public class FileSystemEntry
{
    public string   Name         { get; init; } = string.Empty;
    public string   FullPath     { get; init; } = string.Empty;
    public bool     IsDirectory  { get; init; }
    public long     SizeBytes    { get; init; }   // 0 for directories
    public DateTime LastModified { get; init; }
    public string   Extension    { get; init; } = string.Empty;  // e.g. ".txt"; "" for dirs

    // ── Computed display helpers (bound directly in AXAML) ───────────────────

    /// <summary>
    /// "[DIR]" prefix for directories; six-space indent for files so names
    /// align with the directory label in a monospace font.
    /// </summary>
    public string DisplayName => (IsDirectory ? "[DIR] " : "      ") + Name;

    /// <summary>
    /// Human-readable file size. Directories show "--" because summing
    /// all descendants is a later exercise.
    /// </summary>
    public string DisplaySize => IsDirectory ? "--" : FormatBytes(SizeBytes);

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1_024                  => $"{bytes} B",
        < 1_024 * 1_024          => $"{bytes / 1_024.0:F1} KB",
        < 1_024L * 1_024 * 1_024 => $"{bytes / (1_024.0 * 1_024):F1} MB",
        _                        => $"{bytes / (1_024.0 * 1_024 * 1_024):F1} GB",
    };
}
