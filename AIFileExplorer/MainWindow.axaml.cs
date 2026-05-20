using Avalonia.Controls;
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
    // Expansion is now fully handled by the IsExpanded binding in the AXAML
    // style — no LoadChildren() call needed here anymore.
    //
    // This handler's only remaining job is to tell the ViewModel which
    // directory was selected so it can populate the right-hand file list.

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not DirectoryNodeViewModel node) return;
        if (string.IsNullOrEmpty(node.FullPath)) return;  // guard: Placeholder clicked

        ViewModel.SelectDirectory(node.FullPath);
    }
}
