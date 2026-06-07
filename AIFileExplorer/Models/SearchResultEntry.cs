namespace AIFileExplorer.Models;

/// <summary>
/// A file returned by NL search, paired with a human-readable explanation
/// of which filter criteria it satisfied.
/// </summary>
public class SearchResultEntry
{
    /// <summary>The matched file.</summary>
    public required FileSystemEntry File    { get; init; }

    /// <summary>
    /// One-line summary of why this file matched, e.g. "1.2 MB  ·  05/12/25  ·  type: .pdf".
    /// Displayed below the file name in the search results list.
    /// </summary>
    public required string          Snippet { get; init; }
}
