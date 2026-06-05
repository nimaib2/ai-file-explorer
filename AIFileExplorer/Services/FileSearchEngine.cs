using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIFileExplorer.Models;

namespace AIFileExplorer.Services;

/// <summary>
/// Walks a directory tree and returns all files that satisfy a <see cref="FileQuery"/>.
/// Runs the walk on a thread-pool thread so the UI stays responsive.
/// Directories that cannot be read (access denied, broken symlinks) are silently skipped.
/// Well-known build-artifact and dependency directories are pruned to keep searches fast.
/// </summary>
public class FileSearchEngine
{
    /// <summary>
    /// Directory names that are never searched. These are build artifacts, package caches,
    /// and VCS internals that a user would never store personal files in and that can
    /// contain millions of entries deep in the tree.
    /// Comparison is case-insensitive so this works on both macOS and Windows.
    /// </summary>
    private static readonly HashSet<string> SkippedDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // JS / Node
        "node_modules",
        ".npm",
        ".yarn",
        // .NET
        "bin", "obj",
        ".nuget",
        // Python
        "__pycache__",
        ".venv", "venv", "env", ".env",
        // Java / Gradle / Maven
        ".gradle", ".m2",
        // Version control internals
        ".git", ".svn", ".hg",
        // Generic build / dist output
        "dist", "build", ".next", ".output", ".turbo",
        // macOS
        ".Trash",
        "Library",       // ~/Library is huge and contains no user documents
    };
    /// <summary>
    /// Searches the tree rooted at <see cref="FileQuery.RootPath"/> and returns
    /// every file that passes all active filters.
    /// </summary>
    public Task<List<FileSystemEntry>> SearchAsync(
        FileQuery         query,
        CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            var results = new List<FileSystemEntry>();
            Walk(query.RootPath, query.Filter, results, ct);
            return results;
        }, ct);
    }

    // ── Tree walk ─────────────────────────────────────────────────────────────

    private static void Walk(
        string            dir,
        SearchFilter      filter,
        List<FileSystemEntry> results,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        string[] files;
        string[] subdirs;

        try
        {
            // GetFiles/GetDirectories are eager — they enumerate upfront so the
            // catch here covers all access errors for this directory, including
            // cases where a lazy enumerator would throw mid-iteration (e.g. /etc).
            files   = Directory.GetFiles(dir);
            subdirs = Directory.GetDirectories(dir);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                      or DirectoryNotFoundException
                                      or IOException)
        {
            return; // skip unreadable directories
        }

        foreach (var path in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(path);
                if (Matches(info, filter))
                    results.Add(ToEntry(info));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                // skip individual unreadable files
            }
        }

        foreach (var sub in subdirs)
        {
            if (!SkippedDirNames.Contains(Path.GetFileName(sub)))
                Walk(sub, filter, results, ct);
        }
    }

    // ── Filter predicate ──────────────────────────────────────────────────────

    private static bool Matches(FileInfo file, SearchFilter f)
    {
        // Extension — any one of the listed types must match
        if (f.FileTypes is { Count: > 0 } types)
            if (!types.Contains(file.Extension, StringComparer.OrdinalIgnoreCase))
                return false;

        // Name keywords — ALL keywords must appear in the filename
        if (f.NameKeywords is { Count: > 0 } keywords)
            if (!keywords.All(k => file.Name.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return false;

        // Date range — compare against last-write time (date part only)
        if (f.DateFrom is { } from && file.LastWriteTime.Date < from.Date)
            return false;
        if (f.DateTo   is { } to   && file.LastWriteTime.Date > to.Date)
            return false;

        // Size range
        if (f.SizeMinBytes is { } min && file.Length < min)
            return false;
        if (f.SizeMaxBytes is { } max && file.Length > max)
            return false;

        // Location hint — path fragment that must appear somewhere in the full path
        if (f.LocationHint is { Length: > 0 } hint)
            if (!file.FullName.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return false;

        return true;
    }

    // ── Conversion ────────────────────────────────────────────────────────────

    private static FileSystemEntry ToEntry(FileInfo info) => new()
    {
        Name         = info.Name,
        FullPath     = info.FullName,
        IsDirectory  = false,
        SizeBytes    = info.Length,
        LastModified = info.LastWriteTime,
        Extension    = info.Extension,
    };
}
