namespace AIFileExplorer.Models;

/// <summary>
/// A file returned by NL search, paired with a human-readable explanation
/// of which filter criteria it satisfied.
/// </summary>
public class SearchResultEntry
{
    public required FileSystemEntry File    { get; init; }
    public required string          Snippet { get; init; }
}
