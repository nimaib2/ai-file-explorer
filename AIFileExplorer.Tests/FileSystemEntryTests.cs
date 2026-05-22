using System;
using AIFileExplorer.Models;
using AIFileExplorer.Services;

namespace AIFileExplorer.Tests;

public class FileSystemEntryTests
{
    // ── Icon ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Icon_Directory_ReturnsFolderIcon()
    {
        var entry = new FileSystemEntry { IsDirectory = true };
        Assert.Equal(FileTypeIcons.Folder, entry.Icon);
    }

    [Fact]
    public void Icon_KnownFileExtension_ReturnsCorrectIcon()
    {
        var entry = new FileSystemEntry { IsDirectory = false, Extension = ".cs" };
        Assert.Equal("💻", entry.Icon);
    }

    [Fact]
    public void Icon_UnknownFileExtension_ReturnsGenericIcon()
    {
        var entry = new FileSystemEntry { IsDirectory = false, Extension = ".xyz" };
        Assert.Equal(FileTypeIcons.Generic, entry.Icon);
    }

    // ── DisplaySize ───────────────────────────────────────────────────────────

    [Fact]
    public void DisplaySize_Directory_ReturnsDash()
    {
        var entry = new FileSystemEntry { IsDirectory = true, SizeBytes = 99999 };
        Assert.Equal("--", entry.DisplaySize);
    }

    [Theory]
    [InlineData(0L,               "0 B")]
    [InlineData(512L,             "512 B")]
    [InlineData(1_023L,           "1023 B")]
    [InlineData(1_024L,           "1.0 KB")]
    [InlineData(1_536L,           "1.5 KB")]
    [InlineData(1_048_576L,       "1.0 MB")]
    [InlineData(1_073_741_824L,   "1.0 GB")]
    public void DisplaySize_File_FormatsCorrectly(long bytes, string expected)
    {
        var entry = new FileSystemEntry { IsDirectory = false, SizeBytes = bytes };
        Assert.Equal(expected, entry.DisplaySize);
    }
}
