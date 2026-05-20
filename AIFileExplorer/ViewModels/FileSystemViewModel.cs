using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AIFileExplorer.ViewModels;

/// <summary>
/// Manages the directory tree shown in the left-hand TreeView.
///
/// Single responsibility: own the root nodes and know how to rebuild
/// them when the user navigates to a new path. File-list population
/// (the right pane) is not its concern — that stays in MainWindowViewModel.
///
/// Root initialisation uses the OS to decide what "root" means:
///   macOS/Linux — the user's home directory, labelled "~"
///   Windows     — one entry per ready drive (C:\, D:\, …)
/// </summary>
public partial class FileSystemViewModel : ObservableObject
{
    /// <summary>Bound to TreeView.ItemsSource in the View.</summary>
    [ObservableProperty]
    private ObservableCollection<DirectoryNodeViewModel> _roots = new();

    public FileSystemViewModel()
    {
        // Roots.Add() fires CollectionChanged on the collection, not
        // PropertyChanged on the Roots property, so calling this from the
        // constructor is safe under the PropertyChanged rules.
        InitializeRoots();
    }

    /// <summary>
    /// Rebuilds the tree rooted at <paramref name="path"/>.
    /// Called by MainWindowViewModel when the user types a path and clicks Go.
    /// </summary>
    public void NavigateTo(string path)
    {
        if (!Directory.Exists(path)) return;

        var rootName = Path.GetFileName(path) is { Length: > 0 } n ? n : path;
        var root     = new DirectoryNodeViewModel(rootName, path);
        root.LoadChildren();   // one level eager so the tree isn't empty on arrival

        Roots.Clear();
        Roots.Add(root);
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private void InitializeRoots()
    {
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            // Convention on macOS/Linux: home directory is shown as "~".
            // Environment.SpecialFolder.UserProfile resolves to /Users/<name>
            // on macOS and /home/<name> on Linux — no hardcoded paths.
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var root = new DirectoryNodeViewModel("~", home);
            root.LoadChildren();
            Roots.Add(root);
        }
        else
        {
            // Windows: show every ready drive as its own root entry.
            // DriveInfo.GetDrives() returns all drives; IsReady filters out
            // empty optical drives and disconnected network shares.
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var node = new DirectoryNodeViewModel(
                    drive.Name,
                    drive.RootDirectory.FullName);
                node.LoadChildren();
                Roots.Add(node);
            }
        }
    }
}
