using AIFileExplorer.Services;

namespace AIFileExplorer.Tests;

public class FileTypeIconsTests
{
    // ── Known extensions ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".gif")]
    [InlineData(".svg")]
    public void ForExtension_ImageExtensions_ReturnImageIcon(string ext)
        => Assert.Equal("🖼️", FileTypeIcons.ForExtension(ext));

    [Theory]
    [InlineData(".cs")]
    [InlineData(".py")]
    [InlineData(".ts")]
    [InlineData(".go")]
    public void ForExtension_CodeExtensions_ReturnCodeIcon(string ext)
        => Assert.Equal("💻", FileTypeIcons.ForExtension(ext));

    [Theory]
    [InlineData(".mp3")]
    [InlineData(".wav")]
    [InlineData(".flac")]
    public void ForExtension_AudioExtensions_ReturnAudioIcon(string ext)
        => Assert.Equal("🎵", FileTypeIcons.ForExtension(ext));

    [Theory]
    [InlineData(".mp4")]
    [InlineData(".mov")]
    [InlineData(".mkv")]
    public void ForExtension_VideoExtensions_ReturnVideoIcon(string ext)
        => Assert.Equal("🎬", FileTypeIcons.ForExtension(ext));

    [Theory]
    [InlineData(".zip")]
    [InlineData(".tar")]
    [InlineData(".gz")]
    public void ForExtension_ArchiveExtensions_ReturnArchiveIcon(string ext)
        => Assert.Equal("📦", FileTypeIcons.ForExtension(ext));

    [Theory]
    [InlineData(".json")]
    [InlineData(".yaml")]
    [InlineData(".toml")]
    public void ForExtension_ConfigExtensions_ReturnConfigIcon(string ext)
        => Assert.Equal("⚙️", FileTypeIcons.ForExtension(ext));

    // ── Case insensitivity ────────────────────────────────────────────────────

    [Theory]
    [InlineData(".PNG")]
    [InlineData(".Png")]
    [InlineData(".pNg")]
    public void ForExtension_IsCaseInsensitive(string ext)
        => Assert.Equal(FileTypeIcons.ForExtension(".png"), FileTypeIcons.ForExtension(ext));

    // ── Fallback ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(".xyz")]
    [InlineData(".unknownext")]
    [InlineData("")]
    public void ForExtension_UnknownExtension_ReturnsGenericIcon(string ext)
        => Assert.Equal(FileTypeIcons.Generic, FileTypeIcons.ForExtension(ext));
}
