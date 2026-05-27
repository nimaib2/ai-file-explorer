namespace AIFileExplorer.Models;

/// <summary>
/// Execution unit passed to <c>FileSearchEngine</c>.
/// Combines the structured filter Claude produced with the root directory
/// to walk — kept separate from <see cref="SearchFilter"/> so the engine
/// never needs to know about the Claude layer.
/// </summary>
public class FileQuery
{
    public required string       RootPath { get; init; }
    public required SearchFilter Filter   { get; init; }
}
