using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIFileExplorer.Models;
using AIFileExplorer.Services;

namespace AIFileExplorer.ViewModels;

/// <summary>
/// ViewModel for MainWindow.
///
/// Inherits ObservableObject (from CommunityToolkit.Mvvm) instead of
/// implementing INotifyPropertyChanged by hand. The class must be `partial`
/// so the source generator can add its generated code alongside this file.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly FileSystemService _fs = new();

    // ── [ObservableProperty] fields ───────────────────────────────────────────
    //
    // The source generator reads each [ObservableProperty] field and emits a
    // full public property with a backing field comparison and OnPropertyChanged
    // call — exactly the pattern we wrote by hand before. Naming convention:
    // _camelCase field  →  PascalCase property (strip the leading underscore).
    //
    //   _currentPath  →  CurrentPath
    //   _statusText   →  StatusText
    //   _entries      →  Entries
    //
    // The generated setters already satisfy every PropertyChanged rule:
    //   • Old/new comparison before raising (never raise if unchanged)
    //   • Event raised after the field is updated (end of setter)
    //   • No event raised during construction (field initializers bypass setters)

    [ObservableProperty]
    private string _currentPath =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<FileSystemEntry>? _entries;

    // ── [RelayCommand] ────────────────────────────────────────────────────────
    //
    // The source generator sees [RelayCommand] on Scan() and emits a property:
    //
    //   public IRelayCommand ScanCommand { get; }
    //
    // ScanCommand wraps Scan() and exposes it as a bindable ICommand.
    // The AXAML button binds Command="{Binding ScanCommand}" instead of using
    // Click= in the code-behind, which removes the last piece of logic from
    // MainWindow.axaml.cs and makes the View a pure layout file.

    [RelayCommand]
    private void Scan()
    {
        var path = CurrentPath.Trim();

        if (!Directory.Exists(path))
        {
            // Assign Entries before StatusText so that when StatusText raises
            // PropertyChanged, any observer reading both sees a consistent state.
            Entries    = null;
            StatusText = "Directory not found.";
            return;
        }

        // Finish all computation before writing any observable property.
        // This keeps the ViewModel consistent when PropertyChanged fires.
        var entries   = _fs.WalkDirectory(path, maxDepth: 2).ToList();
        var fileCount = entries.Count(x => !x.IsDirectory);
        var dirCount  = entries.Count(x =>  x.IsDirectory);

        Entries    = entries;
        StatusText = $"{fileCount} files, {dirCount} folders  (depth ≤ 2)";
    }
}
