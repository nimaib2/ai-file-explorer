using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIFileExplorer.Models;
using AIFileExplorer.Services;

namespace AIFileExplorer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly FileSystemService   _fs;
    private readonly FileOperationService _fileOps;
    private readonly IDialogService      _dialogs;

    private List<FileSystemEntry> _allEntries = new();

    // Clipboard state — not observable because the View never displays it.
    // PasteCommand.CanExecute depends on it via HasClipboard(); that CanExecute
    // is manually re-triggered in Copy(), Cut(), and Paste().
    private FileSystemEntry? _clipboardEntry;
    private bool             _clipboardIsCut;

    public FileSystemViewModel FileSystem { get; } = new();

    public MainWindowViewModel(IDialogService dialogs)
    {
        _dialogs  = dialogs;
        _fs       = new FileSystemService();
        _fileOps  = new FileOperationService();
    }

    // ── Observable properties ──────────────────────────────────────────────────

    [ObservableProperty]
    private string _currentPath =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<FileSystemEntry>? _entries;

    // [NotifyCanExecuteChangedFor] tells the generator to call
    // XxxCommand.NotifyCanExecuteChanged() whenever SelectedEntry changes.
    // This re-evaluates each command's CanExecute, which enables or disables
    // the matching context menu items and keyboard shortcuts automatically.
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    [NotifyCanExecuteChangedFor(nameof(CutCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(RenameCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    private FileSystemEntry? _selectedEntry;

    [ObservableProperty]
    private string _fileCountText = "Select a folder in the tree.";

    [ObservableProperty]
    private string _selectionText = string.Empty;

    // ── Partial callbacks ──────────────────────────────────────────────────────

    partial void OnSearchTextChanged(string value)
    {
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

    // ── CanExecute helpers ─────────────────────────────────────────────────────

    private bool HasSelection() => SelectedEntry is not null;
    private bool HasClipboard() => _clipboardEntry is not null;

    // ── Navigation ─────────────────────────────────────────────────────────────

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

    public void SelectDirectory(string path)
    {
        CurrentPath = path;
        LoadEntriesForPath(path);
    }

    // ── File operations ────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Copy()
    {
        _clipboardEntry = SelectedEntry;
        _clipboardIsCut = false;
        PasteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Cut()
    {
        _clipboardEntry = SelectedEntry;
        _clipboardIsCut = true;
        PasteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasClipboard))]
    private void Paste()
    {
        if (_clipboardEntry is not { } entry) return;

        // Moving a directory into the same parent folder it already lives in
        // is a no-op on most filesystems and would throw on others.
        if (_clipboardIsCut &&
            string.Equals(Path.GetDirectoryName(entry.FullPath), CurrentPath,
                          StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            if (_clipboardIsCut)
            {
                _fileOps.MoveTo(entry, CurrentPath);
                // Cut is consumed after a single paste — clear the clipboard.
                _clipboardEntry = null;
                PasteCommand.NotifyCanExecuteChanged();
            }
            else
            {
                _fileOps.CopyTo(entry, CurrentPath);
            }
            Refresh();
        }
        catch (Exception ex) { FileCountText = $"Error: {ex.Message}"; }
    }

    // Delete and Rename are async because they show dialogs before acting.
    // [RelayCommand] on an async Task method generates AsyncRelayCommand,
    // which runs the task on the UI thread and handles exceptions internally.

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Delete()
    {
        if (SelectedEntry is not { } entry) return;

        bool confirmed = await _dialogs.ConfirmDeleteAsync(entry.Name);
        if (!confirmed) return;

        try
        {
            _fileOps.Delete(entry);
            Refresh();
        }
        catch (Exception ex) { FileCountText = $"Error: {ex.Message}"; }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task Rename()
    {
        if (SelectedEntry is not { } entry) return;

        string? newName = await _dialogs.PromptRenameAsync(entry.Name);
        if (string.IsNullOrWhiteSpace(newName) || newName == entry.Name) return;

        try
        {
            _fileOps.Rename(entry, newName);
            Refresh();
        }
        catch (Exception ex) { FileCountText = $"Error: {ex.Message}"; }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Open()
    {
        if (SelectedEntry is not { } entry) return;

        if (entry.IsDirectory)
        {
            SelectDirectory(entry.FullPath);
        }
        else
        {
            try { _fileOps.Open(entry); }
            catch (Exception ex) { FileCountText = $"Error: {ex.Message}"; }
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    // Reloads the current directory after any file operation that changes it.
    private void Refresh() => LoadEntriesForPath(CurrentPath);

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
