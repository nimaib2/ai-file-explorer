using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AIFileExplorer.Services;

namespace AIFileExplorer;

public partial class MainWindow : Window
{
    // One instance of the service — created once, reused on every scan.
    private readonly FileSystemService _fs = new();

    public MainWindow()
    {
        InitializeComponent();

        // Seed the path box with the user's home directory.
        // Environment.GetFolderPath() is cross-platform — no hardcoded "/Users/nimai".
        // Internally it still uses Path.Combine / Path.DirectorySeparatorChar.
        PathBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    // Called when the Scan button is clicked (wired up via Click="OnScanClicked" in AXAML).
    private void OnScanClicked(object? sender, RoutedEventArgs e)
    {
        var path = PathBox.Text?.Trim() ?? string.Empty;

        // Directory.Exists is a quick static check — no DirectoryInfo object needed
        // when you only need a boolean answer.
        if (!Directory.Exists(path))
        {
            StatusText.Text = "Directory not found.";
            FileList.ItemsSource = null;
            return;
        }

        // WalkDirectory returns IEnumerable<FileSystemEntry> lazily.
        // .ToList() forces the full walk to complete now so we can count results
        // and hand a concrete list to the ItemsControl.
        var entries = _fs.WalkDirectory(path, maxDepth: 2).ToList();

        // Setting ItemsSource tells the ItemsControl to render one item per entry.
        // Avalonia reads each entry's properties via the {Binding} expressions
        // in the DataTemplate defined in the AXAML.
        FileList.ItemsSource = entries;

        var fileCount = entries.Count(x => !x.IsDirectory);
        var dirCount  = entries.Count(x =>  x.IsDirectory);
        StatusText.Text = $"{fileCount} files, {dirCount} folders  (depth ≤ 2)";
    }
}
