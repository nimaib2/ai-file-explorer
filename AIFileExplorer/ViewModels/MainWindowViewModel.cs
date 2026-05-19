using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIFileExplorer.Models;
using AIFileExplorer.Services;

namespace AIFileExplorer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly FileSystemService _fs = new();

    // Full unfiltered list for the currently selected directory.
    // Never exposed to the View directly — Entries is the filtered projection.
    private List<FileSystemEntry> _allEntries = new();

    // ── Observable properties ──────────────────────────────────────────────────

    /// <summary>Address bar — the path the user is typing or navigating to.</summary>
    [ObservableProperty]
    private string _currentPath =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Search box text; filters the right-hand file list as you type.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>Root nodes for the left-hand TreeView.</summary>
    [ObservableProperty]
    private ObservableCollection<DirectoryNode> _directoryTree = new();

    /// <summary>Filtered file list for the right-hand pane.</summary>
    [ObservableProperty]
    private IReadOnlyList<FileSystemEntry>? _entries;

    /// <summary>Currently selected row in the right-hand ListBox.</summary>
    [ObservableProperty]
    private FileSystemEntry? _selectedEntry;

    /// <summary>Left side of the status bar: folder/file count after filtering.</summary>
    [ObservableProperty]
    private string _fileCountText = "Enter a path and click Go.";

    /// <summary>Right side of the status bar: details for the selected item.</summary>
    [ObservableProperty]
    private string _selectionText = string.Empty;

    // ── [ObservableProperty] partial callbacks ─────────────────────────────────
    // The toolkit's source generator calls OnXxxChanged(T) after every setter.
    // Implementing the partial method here is the idiomatic way to react to a
    // property change without wiring up a manual PropertyChanged subscription.

    partial void OnSearchTextChanged(string value)
    {
        // Don't filter before the first Navigate() — tree is still empty.
        if (DirectoryTree.Count == 0) return;
        ApplyFilter();
    }

    partial void OnSelectedEntryChanged(FileSystemEntry? value)
    {
        // All computation is done before SelectionText is assigned, satisfying
        // the rule: ViewModel must be in a consistent state when PropertyChanged fires.
        SelectionText = value is null
            ? string.Empty
            : value.IsDirectory
                ? $"[DIR]  {value.Name}"
                : $"{value.Name}  ·  {value.DisplaySize}  ·  {value.LastModified:MM/dd/yy HH:mm}";
    }

    // ── Commands ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to the path in the address bar.
    /// Builds the left-hand tree and loads the directory's direct children
    /// into the right-hand file list.
    /// [RelayCommand] generates a NavigateCommand property bound by the Go button.
    /// </summary>
    [RelayCommand]
    private void Navigate()
    {
        var path = CurrentPath.Trim();

        if (!Directory.Exists(path))
        {
            FileCountText = "Directory not found.";
            Entries       = null;
            return;
        }

        // Build the root node. Use just the final folder name as the label,
        // falling back to the full path for drive roots like "C:\".
        var rootName = Path.GetFileName(path) is { Length: > 0 } n ? n : path;
        var root     = new DirectoryNode(rootName, path);
        root.LoadChildren();   // eagerly load one level so the tree isn't empty

        // Mutate the existing collection instead of replacing it.
        // The TreeView stays subscribed to the same ObservableCollection;
        // CollectionChanged events push the update without needing a new binding.
        DirectoryTree.Clear();
        DirectoryTree.Add(root);

        LoadEntriesForPath(path);
    }

    // ── Called from code-behind when a TreeView node is selected ──────────────

    /// <summary>
    /// Loads the direct children of <paramref name="path"/> into the right pane
    /// and syncs the address bar. Called by MainWindow.axaml.cs on tree selection.
    ///
    /// This is a public method rather than a command because the TreeView's
    /// SelectionChanged event passes the selected node; a parameterised
    /// RelayCommand could work but requires more wiring for this stage.
    /// </summary>
    public void SelectDirectory(string path)
    {
        CurrentPath = path;
        LoadEntriesForPath(path);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void LoadEntriesForPath(string path)
    {
        _allEntries   = _fs.ListDirectory(path).ToList();
        SelectedEntry = null;   // clear before populating — consistent state rule
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        // Complete all computation before touching any observable property so
        // that when PropertyChanged fires, the ViewModel is fully consistent.
        IReadOnlyList<FileSystemEntry> results = string.IsNullOrWhiteSpace(SearchText)
            ? _allEntries
            : _allEntries
                .Where(e => e.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();

        var fileCount = results.Count(x => !x.IsDirectory);
        var dirCount  = results.Count(x =>  x.IsDirectory);

        Entries       = results;
        FileCountText = $"{dirCount} folders,  {fileCount} files";
    }
}
