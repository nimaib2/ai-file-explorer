using System;
using System.Collections.Generic;

namespace AIFileExplorer.Models;

/// <summary>
/// Structured representation of a natural-language file search query,
/// as extracted by Claude. All fields are nullable — absent means "no constraint".
/// </summary>
public class SearchFilter
{
    /// <summary>File extensions to match, e.g. [".pdf", ".docx"]. Null = any type.</summary>
    public List<string>? FileTypes { get; init; }

    /// <summary>Keywords that should appear in the file name. Null = no name constraint.</summary>
    public List<string>? NameKeywords { get; init; }

    /// <summary>Earliest last-modified date (inclusive). Null = no lower bound.</summary>
    public DateTime? DateFrom { get; init; }

    /// <summary>Latest last-modified date (inclusive). Null = no upper bound.</summary>
    public DateTime? DateTo { get; init; }

    /// <summary>Minimum file size in bytes. Null = no lower bound.</summary>
    public long? SizeMinBytes { get; init; }

    /// <summary>Maximum file size in bytes. Null = no upper bound.</summary>
    public long? SizeMaxBytes { get; init; }

    /// <summary>
    /// Path fragment hint, e.g. "Downloads" or "~/Documents".
    /// Null = search everywhere.
    /// </summary>
    public string? LocationHint { get; init; }
}
