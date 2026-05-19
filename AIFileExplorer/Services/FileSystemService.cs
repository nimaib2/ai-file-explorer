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
    /// Each permission-denied path is skipped gracefully rather than throwing,
    /// because it is normal to encounter unreadable directories on macOS/Linux
    /// (e.g. /private/var) and Windows (e.g. System Volume Information).
    /// </summary>
    public IEnumerable<FileSystemEntry> ListDirectory(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists) yield break;

        // Collect both arrays upfront so a permission failure on one does not
        // prevent the other from being returned.
        DirectoryInfo[] subdirs = Array.Empty<DirectoryInfo>();
        FileInfo[]      files   = Array.Empty<FileInfo>();

        try { subdirs = dir.GetDirectories(); } catch (UnauthorizedAccessException) { }
        try { files   = dir.GetFiles();       } catch (UnauthorizedAccessException) { }

        // Directories first — matches the convention of most file explorers.
        foreach (var sub in subdirs.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
            yield return new FileSystemEntry
            {
                Name         = sub.Name,
                FullPath     = sub.FullName,
                IsDirectory  = true,
                LastModified = sub.LastWriteTime,
            };

        foreach (var file in files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
            yield return new FileSystemEntry
            {
                Name         = file.Name,
                FullPath     = file.FullName,
                IsDirectory  = false,
                SizeBytes    = file.Length,
                LastModified = file.LastWriteTime,
                Extension    = file.Extension,
            };
    }
}
