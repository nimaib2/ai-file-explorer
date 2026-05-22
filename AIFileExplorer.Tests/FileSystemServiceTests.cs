using System;
using System.IO;
using System.Linq;
using AIFileExplorer.Services;

namespace AIFileExplorer.Tests;

/// <summary>
/// Tests for FileSystemService.ListDirectory.
/// Each test uses a temporary directory that is cleaned up in Dispose.
/// </summary>
public class FileSystemServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FileSystemService _sut = new();

    public FileSystemServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    // ── Ordering ──────────────────────────────────────────────────────────────

    [Fact]
    public void ListDirectory_ReturnsDirsBeforeFiles()
    {
        File.WriteAllText(Path.Combine(_tempDir, "aaa.txt"), "");
        Directory.CreateDirectory(Path.Combine(_tempDir, "zzz"));

        var (entries, _) = _sut.ListDirectory(_tempDir);

        Assert.True(entries[0].IsDirectory,  "first entry should be the directory");
        Assert.False(entries[1].IsDirectory, "second entry should be the file");
    }

    [Fact]
    public void ListDirectory_SortsAlphabeticallyWithinEachGroup()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "charlie"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "alpha"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "beta"));
        File.WriteAllText(Path.Combine(_tempDir, "zebra.txt"), "");
        File.WriteAllText(Path.Combine(_tempDir, "mango.txt"), "");

        var (entries, _) = _sut.ListDirectory(_tempDir);

        var dirs  = entries.Where(e =>  e.IsDirectory).Select(e => e.Name).ToList();
        var files = entries.Where(e => !e.IsDirectory).Select(e => e.Name).ToList();

        Assert.Equal(new[] { "alpha", "beta", "charlie" }, dirs);
        Assert.Equal(new[] { "mango.txt", "zebra.txt" }, files);
    }

    // ── Metadata ──────────────────────────────────────────────────────────────

    [Fact]
    public void ListDirectory_PopulatesFileMetadata()
    {
        var path = Path.Combine(_tempDir, "hello.txt");
        File.WriteAllText(path, "world");  // 5 bytes

        var (entries, _) = _sut.ListDirectory(_tempDir);
        var entry = entries.Single();

        Assert.Equal("hello.txt",  entry.Name);
        Assert.Equal(path,         entry.FullPath);
        Assert.False(entry.IsDirectory);
        Assert.Equal(5L,           entry.SizeBytes);
        Assert.Equal(".txt",       entry.Extension);
    }

    [Fact]
    public void ListDirectory_PopulatesDirectoryMetadata()
    {
        var subPath = Path.Combine(_tempDir, "subdir");
        Directory.CreateDirectory(subPath);

        var (entries, _) = _sut.ListDirectory(_tempDir);
        var entry = entries.Single();

        Assert.Equal("subdir", entry.Name);
        Assert.Equal(subPath,  entry.FullPath);
        Assert.True(entry.IsDirectory);
        Assert.Equal(0L,       entry.SizeBytes);
        Assert.Equal("",       entry.Extension);
    }

    // ── Edge cases ────────────────────────────────────────────────────────────

    [Fact]
    public void ListDirectory_EmptyDirectory_ReturnsEmptyList()
    {
        var (entries, accessDenied) = _sut.ListDirectory(_tempDir);

        Assert.Empty(entries);
        Assert.False(accessDenied);
    }

    [Fact]
    public void ListDirectory_NonExistentPath_ReturnsEmptyListNoException()
    {
        var (entries, accessDenied) = _sut.ListDirectory("/this/path/does/not/exist");

        Assert.Empty(entries);
        Assert.False(accessDenied);
    }

    [Fact]
    public void ListDirectory_AccessDeniedFlagFalse_ForNormalDirectory()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "");

        var (_, accessDenied) = _sut.ListDirectory(_tempDir);

        Assert.False(accessDenied);
    }
}
