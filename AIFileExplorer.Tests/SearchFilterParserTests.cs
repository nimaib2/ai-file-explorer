using System;
using AIFileExplorer.Services;

namespace AIFileExplorer.Tests;

public class SearchFilterParserTests
{
    // ── Null / empty input ────────────────────────────────────────────────────

    [Fact]
    public void Parse_NullInput_ReturnsEmptyFilter()
    {
        var f = SearchFilterParser.Parse(null);
        Assert.Null(f.FileTypes);
        Assert.Null(f.NameKeywords);
        Assert.Null(f.DateFrom);
        Assert.Null(f.DateTo);
        Assert.Null(f.SizeMinBytes);
        Assert.Null(f.SizeMaxBytes);
        Assert.Null(f.LocationHint);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyFilter()
    {
        var f = SearchFilterParser.Parse("");
        Assert.Null(f.FileTypes);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyFilter()
    {
        var f = SearchFilterParser.Parse("   ");
        Assert.Null(f.FileTypes);
    }

    // ── Malformed JSON ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_InvalidJson_ReturnsEmptyFilter()
    {
        var f = SearchFilterParser.Parse("not json at all");
        Assert.Null(f.FileTypes);
        Assert.Null(f.LocationHint);
    }

    [Fact]
    public void Parse_JsonArray_ReturnsEmptyFilter()
    {
        // Claude returned an array instead of an object
        var f = SearchFilterParser.Parse("[\"wrong\"]");
        Assert.Null(f.FileTypes);
    }

    [Fact]
    public void Parse_JsonString_ReturnsEmptyFilter()
    {
        var f = SearchFilterParser.Parse("\"just a string\"");
        Assert.Null(f.NameKeywords);
    }

    // ── Empty object ──────────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyObject_AllFieldsNull()
    {
        var f = SearchFilterParser.Parse("{}");
        Assert.Null(f.FileTypes);
        Assert.Null(f.NameKeywords);
        Assert.Null(f.DateFrom);
        Assert.Null(f.DateTo);
        Assert.Null(f.SizeMinBytes);
        Assert.Null(f.SizeMaxBytes);
        Assert.Null(f.LocationHint);
    }

    // ── Explicit null fields ──────────────────────────────────────────────────

    [Fact]
    public void Parse_ExplicitNullFields_AllFieldsNull()
    {
        var json = """
            {
              "file_types": null,
              "name_keywords": null,
              "date_from": null,
              "date_to": null,
              "size_min": null,
              "size_max": null,
              "location_hint": null
            }
            """;
        var f = SearchFilterParser.Parse(json);
        Assert.Null(f.FileTypes);
        Assert.Null(f.NameKeywords);
        Assert.Null(f.DateFrom);
        Assert.Null(f.DateTo);
        Assert.Null(f.SizeMinBytes);
        Assert.Null(f.SizeMaxBytes);
        Assert.Null(f.LocationHint);
    }

    // ── Full happy path ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_FullResponse_ExtractsAllFields()
    {
        var json = """
            {
              "file_types":    [".pdf", ".docx"],
              "name_keywords": ["invoice", "2024"],
              "date_from":     "2024-01-01",
              "date_to":       "2024-12-31",
              "size_min":      "10MB",
              "size_max":      "500MB",
              "location_hint": "~/Documents"
            }
            """;
        var f = SearchFilterParser.Parse(json);

        Assert.Equal(new[] { ".pdf", ".docx" }, f.FileTypes);
        Assert.Equal(new[] { "invoice", "2024" }, f.NameKeywords);
        Assert.Equal(new DateTime(2024, 1,  1),  f.DateFrom);
        Assert.Equal(new DateTime(2024, 12, 31), f.DateTo);
        Assert.Equal(10L  * 1024 * 1024,  f.SizeMinBytes);
        Assert.Equal(500L * 1024 * 1024,  f.SizeMaxBytes);
        Assert.Equal("~/Documents",       f.LocationHint);
    }

    // ── Partial responses ─────────────────────────────────────────────────────

    [Fact]
    public void Parse_FileTypesOnly_OtherFieldsNull()
    {
        var json = """{ "file_types": [".png"] }""";
        var f = SearchFilterParser.Parse(json);

        Assert.Equal(new[] { ".png" }, f.FileTypes);
        Assert.Null(f.NameKeywords);
        Assert.Null(f.DateFrom);
    }

    [Fact]
    public void Parse_LocationOnly_OtherFieldsNull()
    {
        var json = """{ "location_hint": "Downloads" }""";
        var f = SearchFilterParser.Parse(json);

        Assert.Equal("Downloads", f.LocationHint);
        Assert.Null(f.FileTypes);
        Assert.Null(f.SizeMinBytes);
    }

    [Fact]
    public void Parse_DateRangeOnly_ParsesCorrectly()
    {
        var json = """{ "date_from": "2025-03-01", "date_to": "2025-03-31" }""";
        var f = SearchFilterParser.Parse(json);

        Assert.Equal(new DateTime(2025, 3,  1), f.DateFrom);
        Assert.Equal(new DateTime(2025, 3, 31), f.DateTo);
        Assert.Null(f.FileTypes);
    }

    // ── Size parsing ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1KB",   1024L)]
    [InlineData("1MB",   1024L * 1024)]
    [InlineData("1GB",   1024L * 1024 * 1024)]
    [InlineData("1B",    1L)]
    [InlineData("500KB", 512_000L)]
    [InlineData("1.5MB", 1_572_864L)]
    public void Parse_SizeStringSuffixes_ConvertsToBytes(string sizeStr, long expectedBytes)
    {
        var json = $$"""{ "size_min": "{{sizeStr}}" }""";
        var f = SearchFilterParser.Parse(json);
        Assert.Equal(expectedBytes, f.SizeMinBytes);
    }

    [Fact]
    public void Parse_SizeAsNumber_TreatedAsBytes()
    {
        var json = """{ "size_min": 2048, "size_max": 65536 }""";
        var f = SearchFilterParser.Parse(json);
        Assert.Equal(2048L,  f.SizeMinBytes);
        Assert.Equal(65536L, f.SizeMaxBytes);
    }

    [Fact]
    public void Parse_UnknownSizeString_ReturnsNull()
    {
        var json = """{ "size_min": "huge" }""";
        var f = SearchFilterParser.Parse(json);
        Assert.Null(f.SizeMinBytes);
    }

    // ── Date edge cases ───────────────────────────────────────────────────────

    [Fact]
    public void Parse_InvalidDateString_ReturnsNull()
    {
        var json = """{ "date_from": "not-a-date" }""";
        var f = SearchFilterParser.Parse(json);
        Assert.Null(f.DateFrom);
    }

    [Fact]
    public void Parse_DateAsNumber_ReturnsNull()
    {
        var json = """{ "date_from": 20240101 }""";
        var f = SearchFilterParser.Parse(json);
        Assert.Null(f.DateFrom);
    }

    // ── Array edge cases ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_EmptyFileTypesArray_ReturnsNull()
    {
        // An empty array means "no constraint" — same as omitting the field
        var json = """{ "file_types": [] }""";
        var f = SearchFilterParser.Parse(json);
        Assert.Null(f.FileTypes);
    }

    [Fact]
    public void Parse_FileTypesNotArray_ReturnsNull()
    {
        // Claude sometimes returns a string instead of an array
        var json = """{ "file_types": ".pdf" }""";
        var f = SearchFilterParser.Parse(json);
        Assert.Null(f.FileTypes);
    }

    // ── Case sensitivity ──────────────────────────────────────────────────────

    [Fact]
    public void Parse_UpperCaseKeys_IgnoredBecauseTryGetPropertyIsCaseSensitive()
    {
        // JsonElement.TryGetProperty does a case-sensitive lookup — the
        // PropertyNameCaseInsensitive option only applies to POCO deserialization.
        // Uppercase keys from Claude won't map to our snake_case fields.
        var json = """{ "FILE_TYPES": [".txt"], "LOCATION_HINT": "Desktop" }""";
        var f = SearchFilterParser.Parse(json);
        Assert.Null(f.FileTypes);
        Assert.Null(f.LocationHint);
    }
}
