using System;
using System.Collections.Generic;
using System.Text.Json;
using AIFileExplorer.Models;

namespace AIFileExplorer.Services;

/// <summary>
/// Parses the raw JSON string returned by Claude into a <see cref="SearchFilter"/>.
/// Kept separate from the API call so it can be unit-tested without network access.
/// </summary>
public static class SearchFilterParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses Claude's JSON response into a <see cref="SearchFilter"/>.
    /// Returns an empty (all-null) filter if the JSON is blank, null, or malformed,
    /// rather than throwing — Claude output can be imperfect.
    /// </summary>
    public static SearchFilter Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new SearchFilter();

        // Claude sometimes wraps its response in markdown code fences despite
        // being told not to. Extract the JSON object between the first { and last }.
        var start = json.IndexOf('{');
        var end   = json.LastIndexOf('}');
        if (start >= 0 && end > start)
            json = json[start..(end + 1)];

        JsonElement root;
        try
        {
            root = JsonSerializer.Deserialize<JsonElement>(json, _jsonOptions);
        }
        catch (JsonException)
        {
            return new SearchFilter();
        }

        if (root.ValueKind != JsonValueKind.Object)
            return new SearchFilter();

        return new SearchFilter
        {
            FileTypes     = ReadStringList(root, "file_types"),
            NameKeywords  = ReadStringList(root, "name_keywords"),
            DateFrom      = ReadDate(root, "date_from"),
            DateTo        = ReadDate(root, "date_to"),
            SizeMinBytes  = ReadBytes(root, "size_min"),
            SizeMaxBytes  = ReadBytes(root, "size_max"),
            LocationHint  = ReadString(root, "location_hint"),
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static List<string>? ReadStringList(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var prop))
            return null;

        if (prop.ValueKind == JsonValueKind.Null)
            return null;

        if (prop.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            var s = item.GetString();
            if (s is not null)
                list.Add(s);
        }

        return list.Count > 0 ? list : null;
    }

    private static DateTime? ReadDate(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var prop))
            return null;

        if (prop.ValueKind is not JsonValueKind.String)
            return null;

        var s = prop.GetString();
        if (s is null)
            return null;

        return DateTime.TryParse(s, out var dt) ? dt : null;
    }

    /// <summary>
    /// Reads a size value. Accepts a raw number (bytes) or a string with a
    /// suffix like "10MB", "500KB", "2GB".
    /// </summary>
    private static long? ReadBytes(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var prop))
            return null;

        if (prop.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        if (prop.ValueKind == JsonValueKind.Number)
            return prop.TryGetInt64(out var n) ? n : null;

        if (prop.ValueKind == JsonValueKind.String)
            return ParseSizeString(prop.GetString());

        return null;
    }

    private static long? ParseSizeString(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;

        s = s.Trim().ToUpperInvariant();

        (string suffix, long multiplier)[] units =
        [
            ("GB",  1L << 30),
            ("MB",  1L << 20),
            ("KB",  1L << 10),
            ("B",   1L),
        ];

        foreach (var (suffix, multiplier) in units)
        {
            if (s.EndsWith(suffix) && double.TryParse(s[..^suffix.Length].Trim(), out var value))
                return (long)(value * multiplier);
        }

        return long.TryParse(s, out var raw) ? raw : null;
    }

    private static string? ReadString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var prop))
            return null;

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }
}
