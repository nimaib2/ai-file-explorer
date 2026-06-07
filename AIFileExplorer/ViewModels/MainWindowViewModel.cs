using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIFileExplorer.Models;
using AIFileExplorer.Services;

namespace AIFileExplorer.ViewModels;

/// <summary>
/// Root ViewModel for the application. Coordinates navigation, file listing,
/// sorting/filtering, file operations, NL search, and the chat sidebar.
/// Each concern is kept in its own region so the class stays readable despite
/// its size — splitting into sub-ViewModels would require cross-VM coupling
/// for commands like OpenSearchResult that need to update both the file list
/// and the current path.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly FileSystemService    _fs;
    private readonly FileOperationService _fileOps;
    private readonly IDialogService       _dialogs;
    private readonly NlSearchService      _nlSearch  = new();
    private readonly FileSearchEngine     _searchEngine = new();

    // Cancelled and replaced whenever a new NL search starts.
    private CancellationTokenSource? _searchCts;

    private List<FileSystemEntry> _allEntries     = new();
    private List<FileSystemEntry> _searchResults  = new();

    // Clipboard state — not observable because the View never displays it.
    // PasteCommand.CanExecute depends on it via HasClipboard(); that CanExecute
    // is manually re-triggered in Copy(), Cut(), and Paste().
    private FileSystemEntry? _clipboardEntry;
    private bool             _clipboardIsCut;

    public FileSystemViewModel FileSystem { get; } = new();
    public ChatViewModel        Chat       { get; }

    public MainWindowViewModel(IDialogService dialogs)
    {
        _dialogs  = dialogs;
        _fs       = new FileSystemService();
        _fileOps  = new FileOperationService();
        Chat      = new ChatViewModel(GetFileContext);
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
    [NotifyCanExecuteChangedFor(nameof(SummarizeCommand))]
    private FileSystemEntry? _selectedEntry;

    /// <summary>True when the selected entry is a readable text/code file.</summary>
    [ObservableProperty]
    private bool _isTextFileSelected;

    // Bound to TreeView.SelectedItem so tree-click navigation stays in the VM.
    // Typed as object? because TreeView.SelectedItem is object? — the callback
    // does the safe cast so compiled bindings don't need a type annotation.
    [ObservableProperty]
    private object? _selectedTreeNode;

    [ObservableProperty]
    private string _fileCountText = "Select a folder in the tree.";

    [ObservableProperty]
    private string _selectionText = string.Empty;

    [ObservableProperty]
    private string _nlQueryText = string.Empty;

    /// <summary>True while the Claude call + file walk are in flight.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearNlSearchCommand))]
    private bool _isSearching;

    /// <summary>True when the right pane is showing NL search results, not a directory listing.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearNlSearchCommand))]
    private bool _isShowingSearchResults;

    /// <summary>Search results with per-entry snippets. Shown in the dedicated search results pane.</summary>
    [ObservableProperty]
    private IReadOnlyList<SearchResultEntry>? _searchResultEntries;

    [ObservableProperty]
    private SearchResultEntry? _selectedSearchResult;

    // Sort state — not individual ObservableProperties because we update both
    // atomically and then call ApplyFilter once. Header labels are computed
    // properties that show the active sort column and direction arrow.
    private string _sortColumn    = "Name";
    private bool   _sortAscending = true;

    public string NameHeader     => SortHeader("Name");
    public string SizeHeader     => SortHeader("Size");
    public string ModifiedHeader => SortHeader("Modified");
    public string ExtHeader      => SortHeader("Ext");

    private string SortHeader(string col)
    {
        if (_sortColumn != col) return col;
        return col + (_sortAscending ? " ↑" : " ↓");
    }

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

        IsTextFileSelected = value is not null && IsTextFile(value);
        UpdateChatContextSummary();
    }

    partial void OnCurrentPathChanged(string value)
    {
        NavigateUpCommand.NotifyCanExecuteChanged();
        UpdateChatContextSummary();
    }

    partial void OnIsShowingSearchResultsChanged(bool value)
    {
        UpdateChatContextSummary();
    }

    partial void OnSelectedTreeNodeChanged(object? value)
    {
        if (value is not DirectoryNodeViewModel node) return;
        if (string.IsNullOrEmpty(node.FullPath)) return;
        SelectDirectory(node.FullPath);
    }

    // ── CanExecute helpers ─────────────────────────────────────────────────────

    private bool HasSelection() => SelectedEntry is not null;
    private bool HasClipboard() => _clipboardEntry is not null;
    private bool HasParent()
    {
        if (string.IsNullOrEmpty(CurrentPath)) return false;
        return Path.GetDirectoryName(CurrentPath) is not null;
    }

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

    [RelayCommand(CanExecute = nameof(HasParent))]
    private void NavigateUp()
    {
        var parent = Path.GetDirectoryName(CurrentPath);
        if (parent is null) return;
        SelectDirectory(parent);
    }

    /// <summary>
    /// Toggles sort direction when the same column is clicked twice;
    /// switches to the new column ascending otherwise.
    /// </summary>
    [RelayCommand]
    private void SortBy(string column)
    {
        if (_sortColumn == column)
            _sortAscending = !_sortAscending;
        else
        {
            _sortColumn    = column;
            _sortAscending = true;
        }

        // Notify all four header properties so the arrow indicator updates.
        OnPropertyChanged(nameof(NameHeader));
        OnPropertyChanged(nameof(SizeHeader));
        OnPropertyChanged(nameof(ModifiedHeader));
        OnPropertyChanged(nameof(ExtHeader));

        ApplyFilter();
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

    // ── Summarize ──────────────────────────────────────────────────────────────

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".rst", ".csv", ".tsv",
        ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf",
        ".cs", ".fs", ".vb", ".py", ".js", ".ts", ".jsx", ".tsx",
        ".java", ".kt", ".swift", ".go", ".rs", ".cpp", ".c", ".h", ".hpp",
        ".sh", ".bash", ".zsh", ".ps1", ".bat", ".rb", ".php", ".lua", ".r", ".sql",
        ".html", ".htm", ".css", ".scss", ".log", ".svg", ".proto", ".graphql",
    };

    private static bool IsTextFile(FileSystemEntry entry) =>
        !entry.IsDirectory && TextExtensions.Contains(entry.Extension);

    [RelayCommand(CanExecute = nameof(CanSummarize))]
    private async Task Summarize()
    {
        if (SelectedEntry is not { } entry || !IsTextFile(entry)) return;

        const int maxBytes = 100 * 1024;
        string content;
        bool wasTruncated;

        try
        {
            var info = new FileInfo(entry.FullPath);
            if (info.Length > maxBytes)
            {
                using var reader = new StreamReader(entry.FullPath);
                var buffer = new char[maxBytes];
                int read = await reader.ReadBlockAsync(buffer, 0, maxBytes);
                content      = new string(buffer, 0, read);
                wasTruncated = true;
            }
            else
            {
                content      = await File.ReadAllTextAsync(entry.FullPath);
                wasTruncated = false;
            }
        }
        catch (Exception ex)
        {
            FileCountText = $"Cannot read file: {ex.Message}";
            return;
        }

        await Chat.SummarizeAsync(entry.Name, content, wasTruncated);
    }

    private bool CanSummarize() => IsTextFileSelected;

    // ── NL search ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NlSearch()
    {
        if (string.IsNullOrWhiteSpace(NlQueryText)) return;

        // Cancel any search already running.
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        IsSearching = true;
        Entries     = null;
        FileCountText = "Asking Claude…";

        try
        {
            var (filter, wasFallback) = await _nlSearch.ParseQueryAsync(NlQueryText, ct);
            Debug.WriteLine($"[NlSearch] Filter — types:[{string.Join(",", filter.FileTypes ?? [])}] keywords:[{string.Join(",", filter.NameKeywords ?? [])}] dateFrom:{filter.DateFrom} dateTo:{filter.DateTo} sizeMin:{filter.SizeMinBytes} sizeMax:{filter.SizeMaxBytes} location:{filter.LocationHint} fallback:{wasFallback}");
            var statusWhileSearching = wasFallback
                ? "Claude timed out — searching by filename…"
                : "Searching files…";
            await Dispatcher.UIThread.InvokeAsync(() => FileCountText = statusWhileSearching);

            var rootPath = ResolveRootPath(filter.LocationHint);
            // LocationHint has been promoted to the root — clear it from the
            // filter so the engine doesn't also apply it as a path-contains check.
            var effectiveFilter = rootPath != CurrentPath
                ? new SearchFilter
                  {
                      FileTypes    = filter.FileTypes,
                      NameKeywords = filter.NameKeywords,
                      DateFrom     = filter.DateFrom,
                      DateTo       = filter.DateTo,
                      SizeMinBytes = filter.SizeMinBytes,
                      SizeMaxBytes = filter.SizeMaxBytes,
                      LocationHint = null,
                  }
                : filter;

            var query   = new FileQuery { RootPath = rootPath, Filter = effectiveFilter };
            var results = await _searchEngine.SearchAsync(query, ct);

            ct.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _searchResults         = results;
                IsShowingSearchResults  = true;
                SearchResultEntries    = results
                    .Select(f => new SearchResultEntry { File = f, Snippet = BuildSnippet(f, effectiveFilter) })
                    .ToList();
                var n = results.Count;
                var suffix = wasFallback ? " (filename search — Claude unavailable)" : string.Empty;
                FileCountText = $"Found {n} file{(n == 1 ? "" : "s")} matching \"{NlQueryText}\"{suffix}";
                ApplyFilter();
            });
        }
        catch (OperationCanceledException)
        {
            // A new search superseded this one — leave the UI to the new one.
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() => FileCountText = $"Search error: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => IsSearching = false);
        }
    }

    private bool CanClearNlSearch() => IsShowingSearchResults || IsSearching;

    [RelayCommand(CanExecute = nameof(CanClearNlSearch))]
    private void ClearNlSearch()
    {
        _searchCts?.Cancel();
        IsShowingSearchResults = false;
        IsSearching            = false;
        LoadEntriesForPath(CurrentPath);
    }

    // ── Search result navigation ───────────────────────────────────────────────

    /// <summary>
    /// Navigates to the clicked search result's parent folder and selects the file.
    /// Called from code-behind on double-tap.
    /// </summary>
    [RelayCommand]
    private void OpenSearchResult()
    {
        if (SelectedSearchResult is not { } result) return;
        var parent = Path.GetDirectoryName(result.File.FullPath);
        if (parent is null) return;
        SelectDirectory(parent);  // clears search-results mode, loads parent dir
        SelectedEntry = _allEntries.FirstOrDefault(e => e.FullPath == result.File.FullPath);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    // Reloads the current directory after any file operation that changes it.
    private void Refresh() => LoadEntriesForPath(CurrentPath);

    /// <summary>
    /// Builds a fresh snapshot of the explorer state for the chat system prompt.
    /// Called by ChatViewModel's Func&lt;FileContext&gt; at the moment the user sends a message.
    /// </summary>
    private FileContext GetFileContext() => new()
    {
        CurrentPath            = CurrentPath,
        SelectedEntry          = SelectedEntry,
        DirectoryEntries       = _allEntries,
        IsShowingSearchResults = IsShowingSearchResults,
        SearchQuery            = IsShowingSearchResults ? NlQueryText : null,
        SearchResults          = _searchResults,
    };

    /// <summary>
    /// Updates the one-line context label shown in the chat panel header.
    /// Called whenever path, selection, or search-results state changes so the
    /// label always reflects what Claude will see on the next message.
    /// </summary>
    private void UpdateChatContextSummary()
    {
        var dirName = Path.GetFileName(CurrentPath.TrimEnd(Path.DirectorySeparatorChar))
                      is { Length: > 0 } n ? n : CurrentPath;

        Chat.ContextSummary = IsShowingSearchResults
            ? $"Search: \"{NlQueryText}\"  ({_searchResults.Count} results)"
            : SelectedEntry is { } sel
                ? $"{dirName}  ·  {sel.Name} selected"
                : dirName;
    }

    /// <summary>
    /// Tries to turn a location hint from Claude into a real directory path.
    /// Priority: absolute path → ~/hint → known special-folder name → home directory.
    /// Falls back to CurrentPath only when the home directory cannot be determined.
    /// When there is no hint, searches from home so queries like "find my resume"
    /// cover the whole user profile, not just the folder currently open in the browser.
    /// </summary>
    private string ResolveRootPath(string? hint)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(hint))
            return Directory.Exists(home) ? home : CurrentPath;

        // 1. Already an absolute path that exists.
        if (Path.IsPathRooted(hint) && Directory.Exists(hint))
            return hint;

        // 2. Relative to home directory (e.g. "Downloads" → ~/Downloads).
        var underHome = Path.Combine(home, hint);
        if (Directory.Exists(underHome))
            return underHome;

        // 3. Known special-folder names — fallback for systems where the folder
        //    is not directly under the home directory (e.g. non-standard Windows setups).
        //    Downloads has no SpecialFolder enum value; it is always found by case 2.
        var known = new Dictionary<string, Environment.SpecialFolder>(StringComparer.OrdinalIgnoreCase)
        {
            ["documents"] = Environment.SpecialFolder.MyDocuments,
            ["desktop"] = Environment.SpecialFolder.Desktop,
            ["pictures"] = Environment.SpecialFolder.MyPictures,
            ["music"] = Environment.SpecialFolder.MyMusic,
            ["videos"] = Environment.SpecialFolder.MyVideos,
        };

        if (known.TryGetValue(hint, out var folder))
        {
            var special = Environment.GetFolderPath(folder);
            if (Directory.Exists(special))
                return special;
        }

        // 4. Unrecognised hint — still better to search the whole home tree than
        //    only the currently-open folder.
        return Directory.Exists(home) ? home : CurrentPath;
    }

    private void LoadEntriesForPath(string path)
    {
        // Navigating always exits search-results mode.
        _searchCts?.Cancel();
        IsSearching            = false;
        IsShowingSearchResults = false;
        _searchResults         = new();
        SearchResultEntries    = null;
        SelectedSearchResult   = null;

        var (entries, accessDenied) = _fs.ListDirectory(path);
        _allEntries   = entries;
        SelectedEntry = null;
        ApplyFilter(accessDenied);
        UpdateChatContextSummary();
    }

    private void ApplyFilter(bool accessDenied = false)
    {
        var source = IsShowingSearchResults ? _searchResults : _allEntries;

        IEnumerable<FileSystemEntry> results = string.IsNullOrWhiteSpace(SearchText)
            ? source
            : source.Where(e =>
                e.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

        // Always keep directories above files, then sort within each group.
        var ordered = results.OrderByDescending(e => e.IsDirectory);
        results = _sortColumn switch
        {
            "Size"     => _sortAscending
                              ? ordered.ThenBy(e => e.SizeBytes)
                              : ordered.ThenByDescending(e => e.SizeBytes),
            "Modified" => _sortAscending
                              ? ordered.ThenBy(e => e.LastModified)
                              : ordered.ThenByDescending(e => e.LastModified),
            "Ext"      => _sortAscending
                              ? ordered.ThenBy(e => e.Extension, StringComparer.OrdinalIgnoreCase)
                              : ordered.ThenByDescending(e => e.Extension, StringComparer.OrdinalIgnoreCase),
            _          => _sortAscending    // "Name" (default)
                              ? ordered.ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                              : ordered.ThenByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase),
        };

        var list      = results.ToList();

        Entries = list;

        // In search-results mode the caller owns the status text ("Found X files matching…").
        // Only update FileCountText for normal directory listings.
        if (!IsShowingSearchResults)
        {
            var fileCount = list.Count(x => !x.IsDirectory);
            var dirCount  = list.Count(x =>  x.IsDirectory);
            FileCountText = accessDenied
                ? $"{dirCount} folders,  {fileCount} files  ⚠ Some items could not be read (permission denied)"
                : $"{dirCount} folders,  {fileCount} files";
        }
    }

    /// <summary>
    /// Builds a one-line "why this file matched" string from the file's actual
    /// metadata and the filter conditions that were active for this search.
    /// </summary>
    private static string BuildSnippet(FileSystemEntry file, SearchFilter filter)
    {
        var parts = new List<string>();

        // Always lead with the file's size and last-modified date.
        parts.Add(file.DisplaySize);
        parts.Add(file.LastModified.ToString("MM/dd/yy HH:mm"));

        if (filter.FileTypes?.Count > 0)
            parts.Add($"type: {file.Extension}");

        if (filter.NameKeywords?.Count > 0)
            parts.Add($"name: {string.Join(", ", filter.NameKeywords)}");

        if (filter.DateFrom.HasValue && filter.DateTo.HasValue)
            parts.Add($"range: {filter.DateFrom:MM/dd/yy}–{filter.DateTo:MM/dd/yy}");
        else if (filter.DateFrom.HasValue)
            parts.Add($"after {filter.DateFrom:MM/dd/yy}");
        else if (filter.DateTo.HasValue)
            parts.Add($"before {filter.DateTo:MM/dd/yy}");

        if (filter.SizeMinBytes.HasValue && filter.SizeMaxBytes.HasValue)
            parts.Add($"size filter: {filter.SizeMinBytes / (1 << 20)} MB – {filter.SizeMaxBytes / (1 << 20)} MB");
        else if (filter.SizeMinBytes.HasValue)
            parts.Add($"size > {filter.SizeMinBytes / (1 << 20)} MB");
        else if (filter.SizeMaxBytes.HasValue)
            parts.Add($"size < {filter.SizeMaxBytes / (1 << 20)} MB");

        return string.Join("  ·  ", parts);
    }
}
