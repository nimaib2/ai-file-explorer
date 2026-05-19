using Avalonia.Controls;
using AIFileExplorer.Models;
using AIFileExplorer.ViewModels;

namespace AIFileExplorer;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainWindowViewModel();
    }

    // ── TreeView selection ─────────────────────────────────────────────────────
    //
    // This handler lives in code-behind rather than the ViewModel for two reasons:
    //
    //   1. It has a UI concern: calling node.LoadChildren() is about lazily
    //      populating tree nodes — that's a View-layer decision about how much
    //      data to load based on user interaction with a specific control.
    //
    //   2. SelectionChangedEventArgs gives us the actual DirectoryNode object
    //      directly, whereas a RelayCommand would need extra plumbing to receive
    //      a command parameter from the TreeView.
    //
    // After the UI concern is handled, everything else is delegated to the ViewModel.

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not DirectoryNode node) return;
        if (string.IsNullOrEmpty(node.FullPath)) return;   // guard against Placeholder

        // Lazy-load this node's subdirectory children so they appear when the
        // user expands it next. Idempotent — safe to call on every selection.
        node.LoadChildren();

        // Hand off to the ViewModel for all data/state concerns.
        ViewModel.SelectDirectory(node.FullPath);
    }
}
