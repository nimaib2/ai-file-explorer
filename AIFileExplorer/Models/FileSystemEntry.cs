using System;

namespace AIFileExplorer.Models;

/// <summary>
/// Represents a single file or directory discovered during a directory walk.
/// This is a plain data class — no Avalonia or UI dependencies.
/// </summary>
public class FileSystemEntry
{
    public string Name { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public bool IsDirectory { get; init; }
    public long SizeBytes { get; init; }       // always 0 for directories
    public DateTime LastModified { get; init; }
    public string Extension { get; init; } = string.Empty;  // e.g. ".txt"; "" for dirs
    public int Depth { get; init; }            // 0 = root, 1 = first level, etc.

    // ── Computed display helpers (used directly by AXAML data-binding) ────────

    /// <summary>
    /// Name prefixed with spaces to simulate tree indentation.
    /// Four spaces per depth level looks clean in a monospace font.
    /// "[DIR]" vs "     " visually separates folders from files.
    /// </summary>
    public string DisplayName =>
        new string(' ', Depth * 4) + (IsDirectory ? "[DIR] " : "      ") + Name;

    /// <summary>
    /// Directories don't have a meaningful size here (that would require
    /// summing all descendants — a later exercise). Files show KB/MB/GB.
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
