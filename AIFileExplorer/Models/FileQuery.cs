namespace AIFileExplorer.Models;

/// <summary>
/// Execution unit passed to <c>FileSearchEngine</c>.
/// Combines the structured filter Claude produced with the root directory
/// to walk — kept separate from <see cref="SearchFilter"/> so the engine
/// never needs to know about the Claude layer.
/// </summary>
public class FileQuery
{
    /// <summary>Absolute path of the directory to walk. Must exist.</summary>
    public required string       RootPath { get; init; }

    /// <summary>Constraints to apply to each file encountered during the walk.</summary>
    public required SearchFilter Filter   { get; init; }
}
