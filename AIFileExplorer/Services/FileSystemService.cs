using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AIFileExplorer.Models;

namespace AIFileExplorer.Services;

/// <summary>
/// Wraps System.IO APIs for reading directory contents.
/// All metadata access goes through DirectoryInfo/FileInfo rather than
/// the static Directory/File helpers, so each entry's metadata is read
/// in one OS call rather than several separate ones.
/// </summary>
public class FileSystemService
{
    /// <summary>
    /// Returns the direct children of <paramref name="path"/>: subdirectories
    /// first (alphabetical), then files (alphabetical). Does not recurse.
    ///
    /// Returns a tuple so callers can show a friendly "access denied" message
    /// when either the directory list or file list was blocked by the OS.
    /// Using an eager List (not a lazy iterator) means we can detect the
    /// access-denied condition and return it alongside the partial results.
    /// </summary>
    public (List<FileSystemEntry> Entries, bool AccessDenied) ListDirectory(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists) return (new List<FileSystemEntry>(), false);

        DirectoryInfo[] subdirs    = Array.Empty<DirectoryInfo>();
        FileInfo[]      files      = Array.Empty<FileInfo>();
        bool            accessDenied = false;

        try { subdirs = dir.GetDirectories(); }
        catch (UnauthorizedAccessException) { accessDenied = true; }

        try { files = dir.GetFiles(); }
        catch (UnauthorizedAccessException) { accessDenied = true; }

        var entries = new List<FileSystemEntry>(subdirs.Length + files.Length);

        // Directories first — matches the convention of most file explorers.
        foreach (var sub in subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            entries.Add(new FileSystemEntry
            {
                Name         = sub.Name,
                FullPath     = sub.FullName,
                IsDirectory  = true,
                LastModified = sub.LastWriteTime,
            });

        foreach (var file in files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            entries.Add(new FileSystemEntry
            {
                Name         = file.Name,
                FullPath     = file.FullName,
                IsDirectory  = false,
                SizeBytes    = file.Length,
                LastModified = file.LastWriteTime,
                Extension    = file.Extension,
            });

        return (entries, accessDenied);
    }
}
