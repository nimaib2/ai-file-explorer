using System;
using System.Diagnostics;
using System.IO;
using AIFileExplorer.Models;

namespace AIFileExplorer.Services;

/// <summary>
/// Wraps System.IO file operations — Copy, Move, Delete, Rename, Open.
/// Keeps raw IO calls out of the ViewModel so each operation is testable
/// and replaceable without touching any UI code.
///
/// Directory copies are not a single BCL call, so CopyDirectoryRecursive
/// handles them manually. Everything else delegates directly to File/Directory.
/// </summary>
public class FileOperationService
{
    /// <summary>
    /// Copies a file or directory into <paramref name="destDirectory"/>.
    /// Overwrites any existing file at the destination path.
    /// </summary>
    public void CopyTo(FileSystemEntry entry, string destDirectory)
    {
        var dest = Path.Combine(destDirectory, entry.Name);
        if (entry.IsDirectory)
            CopyDirectoryRecursive(entry.FullPath, dest);
        else
            File.Copy(entry.FullPath, dest, overwrite: true);
    }

    /// <summary>
    /// Moves a file or directory into <paramref name="destDirectory"/>.
    /// For files, overwrites any existing file at the destination.
    /// </summary>
    public void MoveTo(FileSystemEntry entry, string destDirectory)
    {
        var dest = Path.Combine(destDirectory, entry.Name);
        if (entry.IsDirectory)
            Directory.Move(entry.FullPath, dest);
        else
            File.Move(entry.FullPath, dest, overwrite: true);
    }

    /// <summary>
    /// Permanently deletes a file or a directory and all its contents.
    /// The caller is responsible for showing a confirmation dialog first.
    /// </summary>
    public void Delete(FileSystemEntry entry)
    {
        if (entry.IsDirectory)
            Directory.Delete(entry.FullPath, recursive: true);
        else
            File.Delete(entry.FullPath);
    }

    /// <summary>
    /// Renames an entry in-place by moving it to a new name in the same
    /// parent directory. Uses Directory.Move / File.Move rather than a
    /// copy+delete pair so it is atomic on most filesystems.
    /// </summary>
    public void Rename(FileSystemEntry entry, string newName)
    {
        var parent = Path.GetDirectoryName(entry.FullPath)
            ?? throw new InvalidOperationException("Cannot determine parent directory.");
        var dest = Path.Combine(parent, newName);

        if (entry.IsDirectory)
            Directory.Move(entry.FullPath, dest);
        else
            File.Move(entry.FullPath, dest);
    }

    /// <summary>
    /// Opens a file or directory using the OS default handler.
    /// UseShellExecute = true is required for this cross-platform — it tells
    /// the OS to pick the right application rather than treating the path as
    /// a raw executable.
    /// </summary>
    public void Open(FileSystemEntry entry)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName        = entry.FullPath,
            UseShellExecute = true,
        });
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static void CopyDirectoryRecursive(string src, string dest)
    {
        Directory.CreateDirectory(dest);

        foreach (var file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);

        foreach (var dir in Directory.GetDirectories(src))
            CopyDirectoryRecursive(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }
}
