using System;
using System.Collections.Generic;
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
    private List<FileSystemEntry> _allEntries = new();

    // ── Composed ViewModels ────────────────────────────────────────────────────

    /// <summary>
    /// Owns the left-hand tree. Exposed as a property so the View can bind
    /// directly to FileSystem.Roots without MainWindowViewModel re-exposing it.
    /// </summary>
    public FileSystemViewModel FileSystem { get; } = new();

    // ── Observable properties ──────────────────────────────────────────────────

    [ObservableProperty]
    private string _currentPath =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<FileSystemEntry>? _entries;

    [ObservableProperty]
    private FileSystemEntry? _selectedEntry;

    [ObservableProperty]
    private string _fileCountText = "Select a folder in the tree.";

    [ObservableProperty]
    private string _selectionText = string.Empty;

    // ── Partial callbacks ──────────────────────────────────────────────────────

    partial void OnSearchTextChanged(string value)
    {
        // Entries is null until the first directory is loaded; don't filter yet.
        if (Entries is null) return;
        ApplyFilter();
    }

    partial void OnSelectedEntryChanged(FileSystemEntry? value)
    {
        SelectionText = value is null
            ? string.Empty
            : value.IsDirectory
                ? $"[DIR]  {value.Name}"
                : $"{value.Name}  ·  {value.DisplaySize}  ·  {value.LastModified:MM/dd/yy HH:mm}";
    }

    // ── Commands ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates both panes to the path typed in the address bar.
    /// Rebuilds the tree via FileSystemViewModel and reloads the right pane.
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

        FileSystem.NavigateTo(path);
        LoadEntriesForPath(path);
    }

    // ── Called from code-behind on tree selection ──────────────────────────────

    public void SelectDirectory(string path)
    {
        CurrentPath = path;
        LoadEntriesForPath(path);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void LoadEntriesForPath(string path)
    {
        _allEntries   = _fs.ListDirectory(path).ToList();
        SelectedEntry = null;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
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
