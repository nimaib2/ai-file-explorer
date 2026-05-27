using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AIFileExplorer.Models;
using AIFileExplorer.Services;

namespace AIFileExplorer.Tests;

/// <summary>
/// Tests for FileSearchEngine.SearchAsync.
/// Each test creates a fresh temp directory tree that is cleaned up in Dispose.
/// </summary>
public class FileSearchEngineTests : IDisposable
{
    private readonly string          _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly FileSearchEngine _sut  = new();

    public FileSearchEngineTests() => Directory.CreateDirectory(_root);
    public void Dispose()          => Directory.Delete(_root, recursive: true);

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string MakeFile(string relativePath, string content = "x", DateTime? lastWrite = null)
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        if (lastWrite.HasValue)
            File.SetLastWriteTime(full, lastWrite.Value);
        return full;
    }

    private async Task<List<FileSystemEntry>> Search(SearchFilter filter)
    {
        var query = new FileQuery { RootPath = _root, Filter = filter };
        return await _sut.SearchAsync(query);
    }

    // ── No filter ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_NoFilters_ReturnsAllFiles()
    {
        MakeFile("a.txt");
        MakeFile("sub/b.pdf");

        var results = await Search(new SearchFilter());

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Search_EmptyTree_ReturnsEmpty()
    {
        var results = await Search(new SearchFilter());
        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_NonExistentRoot_ReturnsEmpty()
    {
        var query = new FileQuery
        {
            RootPath = Path.Combine(_root, "does-not-exist"),
            Filter   = new SearchFilter()
        };
        var results = await _sut.SearchAsync(query);
        Assert.Empty(results);
    }

    // ── Extension filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task Search_FileTypeFilter_ReturnsOnlyMatchingExtensions()
    {
        MakeFile("report.pdf");
        MakeFile("notes.txt");
        MakeFile("sub/data.pdf");

        var results = await Search(new SearchFilter { FileTypes = [".pdf"] });

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(".pdf", r.Extension));
    }

    [Fact]
    public async Task Search_MultipleFileTypes_MatchesAny()
    {
        MakeFile("a.pdf");
        MakeFile("b.docx");
        MakeFile("c.txt");

        var results = await Search(new SearchFilter { FileTypes = [".pdf", ".docx"] });

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Search_FileTypeFilter_CaseInsensitive()
    {
        MakeFile("photo.JPG");

        var results = await Search(new SearchFilter { FileTypes = [".jpg"] });

        Assert.Single(results);
    }

    // ── Name keyword filter ───────────────────────────────────────────────────

    [Fact]
    public async Task Search_NameKeyword_ReturnsMatchingFiles()
    {
        MakeFile("invoice_2024.pdf");
        MakeFile("receipt_2024.pdf");
        MakeFile("notes.txt");

        var results = await Search(new SearchFilter { NameKeywords = ["invoice"] });

        Assert.Single(results);
        Assert.Equal("invoice_2024.pdf", results[0].Name);
    }

    [Fact]
    public async Task Search_MultipleKeywords_RequiresAllToMatch()
    {
        MakeFile("invoice_jan.pdf");
        MakeFile("invoice_feb.pdf");
        MakeFile("receipt_jan.pdf");

        var results = await Search(new SearchFilter { NameKeywords = ["invoice", "jan"] });

        Assert.Single(results);
        Assert.Equal("invoice_jan.pdf", results[0].Name);
    }

    [Fact]
    public async Task Search_NameKeyword_CaseInsensitive()
    {
        MakeFile("Invoice.pdf");

        var results = await Search(new SearchFilter { NameKeywords = ["INVOICE"] });

        Assert.Single(results);
    }

    // ── Date filter ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_DateFrom_ExcludesOlderFiles()
    {
        MakeFile("old.txt",    lastWrite: new DateTime(2023, 1, 1));
        MakeFile("recent.txt", lastWrite: new DateTime(2025, 6, 1));

        var results = await Search(new SearchFilter { DateFrom = new DateTime(2025, 1, 1) });

        Assert.Single(results);
        Assert.Equal("recent.txt", results[0].Name);
    }

    [Fact]
    public async Task Search_DateTo_ExcludesNewerFiles()
    {
        MakeFile("old.txt",    lastWrite: new DateTime(2023, 1, 1));
        MakeFile("recent.txt", lastWrite: new DateTime(2025, 6, 1));

        var results = await Search(new SearchFilter { DateTo = new DateTime(2024, 12, 31) });

        Assert.Single(results);
        Assert.Equal("old.txt", results[0].Name);
    }

    [Fact]
    public async Task Search_DateRange_IncludesBoundaryDates()
    {
        MakeFile("exact.txt", lastWrite: new DateTime(2024, 6, 15));

        var results = await Search(new SearchFilter
        {
            DateFrom = new DateTime(2024, 6, 15),
            DateTo   = new DateTime(2024, 6, 15),
        });

        Assert.Single(results);
    }

    // ── Size filter ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_SizeMin_ExcludesSmallFiles()
    {
        MakeFile("small.txt", "hi");                     // 2 bytes
        MakeFile("large.txt", new string('x', 10_000));  // 10 KB

        var results = await Search(new SearchFilter { SizeMinBytes = 1_000 });

        Assert.Single(results);
        Assert.Equal("large.txt", results[0].Name);
    }

    [Fact]
    public async Task Search_SizeMax_ExcludesLargeFiles()
    {
        MakeFile("small.txt", "hi");
        MakeFile("large.txt", new string('x', 10_000));

        var results = await Search(new SearchFilter { SizeMaxBytes = 100 });

        Assert.Single(results);
        Assert.Equal("small.txt", results[0].Name);
    }

    // ── Location hint filter ──────────────────────────────────────────────────

    [Fact]
    public async Task Search_LocationHint_OnlyMatchesFilesInMatchingSubpath()
    {
        MakeFile("downloads/report.pdf");
        MakeFile("documents/notes.txt");

        var results = await Search(new SearchFilter { LocationHint = "downloads" });

        Assert.Single(results);
        Assert.Equal("report.pdf", results[0].Name);
    }

    [Fact]
    public async Task Search_LocationHint_CaseInsensitive()
    {
        MakeFile("Downloads/file.txt");

        var results = await Search(new SearchFilter { LocationHint = "DOWNLOADS" });

        Assert.Single(results);
    }

    // ── Combined filters ──────────────────────────────────────────────────────

    [Fact]
    public async Task Search_CombinedFilters_AllMustPass()
    {
        MakeFile("downloads/invoice.pdf", new string('x', 5_000),
            lastWrite: new DateTime(2024, 3, 1));
        MakeFile("downloads/receipt.pdf", new string('x', 5_000),
            lastWrite: new DateTime(2024, 3, 1));
        MakeFile("documents/invoice.txt", new string('x', 5_000),
            lastWrite: new DateTime(2024, 3, 1));

        var results = await Search(new SearchFilter
        {
            FileTypes    = [".pdf"],
            NameKeywords = ["invoice"],
            LocationHint = "downloads",
        });

        Assert.Single(results);
        Assert.Equal("invoice.pdf", results[0].Name);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_CancelledToken_ThrowsOperationCanceledException()
    {
        // Create enough files that the walk has work to do
        for (var i = 0; i < 20; i++)
            MakeFile($"file{i}.txt");

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancel immediately

        var query = new FileQuery { RootPath = _root, Filter = new SearchFilter() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.SearchAsync(query, cts.Token));
    }

    // ── Metadata ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_ReturnedEntries_HaveCorrectMetadata()
    {
        var path = MakeFile("sub/report.pdf", "hello");

        var results = await Search(new SearchFilter { FileTypes = [".pdf"] });

        Assert.Single(results);
        var entry = results[0];
        Assert.Equal("report.pdf", entry.Name);
        Assert.Equal(path,         entry.FullPath);
        Assert.False(entry.IsDirectory);
        Assert.Equal(5L,           entry.SizeBytes);  // "hello" = 5 bytes
        Assert.Equal(".pdf",       entry.Extension);
    }

    // ── Recursive walk ────────────────────────────────────────────────────────

    [Fact]
    public async Task Search_FindsFilesInNestedSubdirectories()
    {
        MakeFile("a/b/c/deep.txt");

        var results = await Search(new SearchFilter());

        Assert.Single(results);
        Assert.Equal("deep.txt", results[0].Name);
    }
}
