using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace AIFileExplorer.Models;

/// <summary>
/// A node in the directory tree shown in the left-hand TreeView.
///
/// Children are loaded lazily: a placeholder entry is inserted when the node
/// is first created so Avalonia's TreeView renders an expand arrow. When the
/// node is selected, LoadChildren() swaps the placeholder for real subdirectory
/// nodes. ObservableCollection ensures the TreeView reacts to that swap.
/// </summary>
public class DirectoryNode
{
    /// <summary>
    /// Sentinel child added to any node that has subdirectories but hasn't been
    /// expanded yet. Its presence makes the TreeView show an expand arrow.
    /// It is never displayed directly — LoadChildren() clears it immediately.
    /// </summary>
    public static readonly DirectoryNode Placeholder = new("…", string.Empty);

    public string Name     { get; }
    public string FullPath { get; }

    public ObservableCollection<DirectoryNode> Children { get; } = new();

    private bool _loaded;

    public DirectoryNode(string name, string fullPath)
    {
        Name     = name;
        FullPath = fullPath;
    }

    /// <summary>
    /// Replaces the placeholder with real subdirectory nodes.
    /// Idempotent — safe to call more than once; only runs the first time.
    /// </summary>
    public void LoadChildren()
    {
        // Placeholder nodes have an empty FullPath; guard against recursion on them.
        if (_loaded || string.IsNullOrEmpty(FullPath)) return;
        _loaded = true;
        Children.Clear();

        string[] subdirs;
        try   { subdirs = Directory.GetDirectories(FullPath); }
        catch (UnauthorizedAccessException) { return; }

        foreach (var dir in subdirs.OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var info  = new DirectoryInfo(dir);
            var child = new DirectoryNode(info.Name, info.FullName);

            // Peek one level to decide whether the child needs its own expand arrow.
            bool hasSubs = false;
            try   { hasSubs = Directory.GetDirectories(dir).Length > 0; }
            catch (UnauthorizedAccessException) { }

            if (hasSubs) child.Children.Add(Placeholder);
            Children.Add(child);
        }
    }
}
