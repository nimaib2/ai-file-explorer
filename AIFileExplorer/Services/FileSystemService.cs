using System;
using System.Collections.Generic;
using System.IO;
using AIFileExplorer.Models;

namespace AIFileExplorer.Services;

/// <summary>
/// Wraps System.IO APIs for directory walking and file reading.
/// All path construction goes through Path.Combine() — never "/" or "\".
/// </summary>
public class FileSystemService
{
    /// <summary>
    /// Entry point for a recursive directory walk.
    ///
    /// Returns an IEnumerable produced by an *iterator method* (WalkRecursive).
    /// That means entries are generated one at a time as the caller iterates —
    /// no giant list is built in memory upfront. The `yield return` keyword
    /// inside WalkRecursive is what makes it an iterator.
    /// </summary>
    public IEnumerable<FileSystemEntry> WalkDirectory(string rootPath, int maxDepth = 3)
    {
        // DirectoryInfo wraps a path and exposes rich metadata (Exists, Name,
        // LastWriteTime, GetFiles(), GetDirectories(), …) without requiring
        // separate static calls to Directory.* for each piece of info.
        var rootInfo = new DirectoryInfo(rootPath);

        if (!rootInfo.Exists)
            yield break;   // 'yield break' exits an iterator — like 'return' but for IEnumerable

        foreach (var entry in WalkRecursive(rootInfo, depth: 0, maxDepth))
            yield return entry;
    }

    // Private recursive helper. Each call handles one directory level.
    private static IEnumerable<FileSystemEntry> WalkRecursive(
        DirectoryInfo dir, int depth, int maxDepth)
    {
        // Yield the directory node BEFORE its children — this is called
        // "pre-order" traversal. The tree prints parent → children naturally.
        yield return new FileSystemEntry
        {
            Name         = dir.Name,
            FullPath     = dir.FullName,   // absolute, OS-native path
            IsDirectory  = true,
            LastModified = dir.LastWriteTime,
            Depth        = depth,
        };

        if (depth >= maxDepth)
            yield break;  // hit the depth limit — do not recurse further

        // ── Files inside this directory ───────────────────────────────────────
        // dir.GetFiles() returns FileInfo[] — each object already has
        // .Length (bytes), .LastWriteTime, .Extension populated by the OS.
        // Contrast with Directory.GetFiles() which returns plain strings
        // and requires a separate FileInfo() call to get metadata.
        FileInfo[] files;
        try
        {
            files = dir.GetFiles();
        }
        catch (UnauthorizedAccessException)
        {
            yield break;  // skip directories we can't read (e.g. /private/var on macOS)
        }

        foreach (var file in files)
        {
            yield return new FileSystemEntry
            {
                Name         = file.Name,
                FullPath     = file.FullName,
                IsDirectory  = false,
                SizeBytes    = file.Length,         // bytes on disk
                LastModified = file.LastWriteTime,
                Extension    = file.Extension,      // includes the dot: ".txt", ".cs"
                Depth        = depth + 1,
            };
        }

        // ── Subdirectories ────────────────────────────────────────────────────
        DirectoryInfo[] subdirs;
        try
        {
            subdirs = dir.GetDirectories();
        }
        catch (UnauthorizedAccessException)
        {
            yield break;
        }

        foreach (var subdir in subdirs)
        {
            // Each recursive call itself returns an IEnumerable, so we
            // re-yield every entry from it with `foreach + yield return`.
            foreach (var entry in WalkRecursive(subdir, depth + 1, maxDepth))
                yield return entry;
        }
    }

    /// <summary>
    /// Reads the full text of a file into a string.
    ///
    /// File.ReadAllText is fine for small/medium files. For very large files
    /// (logs, CSV exports) you would use a StreamReader to read line-by-line
    /// and avoid loading gigabytes into RAM at once.
    /// </summary>
    public string ReadFileText(string path) => File.ReadAllText(path);

    /// <summary>
    /// Flat search — returns an array of absolute path strings for all files
    /// matching an extension anywhere under a directory.
    ///
    /// SearchOption.AllDirectories tells GetFiles() to recurse automatically.
    /// Use this when you want a flat list; use WalkDirectory() when you need
    /// the tree structure or per-file metadata.
    /// </summary>
    public string[] FindByExtension(string directory, string extension) =>
        Directory.GetFiles(directory, $"*{extension}", SearchOption.AllDirectories);

    /// <summary>
    /// Demonstrates Path.Combine() — the correct, cross-platform way to build
    /// file paths. On macOS/Linux it uses '/'; on Windows it uses '\'.
    /// Path.DirectorySeparatorChar is the single char version of that separator.
    ///
    /// Examples of what NOT to do:
    ///   bad:  "/Users/nimai" + "/" + "Documents"   ← hardcoded separator
    ///   good: Path.Combine("/Users/nimai", "Documents")
    /// </summary>
    public static string JoinPath(params string[] parts) => Path.Combine(parts);
}
