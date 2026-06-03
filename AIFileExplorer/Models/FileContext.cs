using System.Collections.Generic;

namespace AIFileExplorer.Models;

/// <summary>
/// Snapshot of the explorer state at the moment the user sends a chat message.
/// Passed to <c>ChatService</c> so it can build an accurate system prompt that
/// grounds Claude's answers in what the user is actually looking at.
/// </summary>
public class FileContext
{
    /// <summary>The directory currently open in the file browser.</summary>
    public string CurrentPath { get; init; } = string.Empty;

    /// <summary>The file or folder the user has highlighted, or null if nothing is selected.</summary>
    public FileSystemEntry? SelectedEntry { get; init; }

    /// <summary>
    /// All entries in the current directory (unfiltered).
    /// Used to answer "what kind of files are in this folder?" questions.
    /// </summary>
    public IReadOnlyList<FileSystemEntry> DirectoryEntries { get; init; } = [];

    /// <summary>True when the right pane is showing NL search results.</summary>
    public bool IsShowingSearchResults { get; init; }

    /// <summary>The NL query that produced the current search results, if any.</summary>
    public string? SearchQuery { get; init; }

    /// <summary>The files returned by the most recent NL search.</summary>
    public IReadOnlyList<FileSystemEntry> SearchResults { get; init; } = [];
}
