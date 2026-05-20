using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using AIFileExplorer.Services;
using AIFileExplorer.ViewModels;
using AIFileExplorer.Views;

namespace AIFileExplorer;

public partial class MainWindow : Window
{
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        InitializeComponent();
        // IDialogService is implemented here (View layer) because showing a
        // dialog requires a parent Window reference. The ViewModel gets an
        // interface — it never knows that Avalonia Windows are involved.
        DataContext = new MainWindowViewModel(new DialogService(this));
    }

    // ── TreeView selection ─────────────────────────────────────────────────────

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (e.AddedItems[0] is not DirectoryNodeViewModel node) return;
        if (string.IsNullOrEmpty(node.FullPath)) return;

        ViewModel.SelectDirectory(node.FullPath);
    }

    // ── File list double-tap ───────────────────────────────────────────────────
    // Double-tapping an entry opens it: navigates into directories,
    // launches files with their OS default application.

    private void OnFileDoubleTapped(object? sender, TappedEventArgs e)
        => ViewModel.OpenCommand.Execute(null);

    // ── IDialogService implementation ──────────────────────────────────────────
    //
    // Nested here because it is a View concern: it creates Avalonia Window
    // objects and calls ShowDialog. None of that belongs in the ViewModel.
    // The ViewModel only sees the IDialogService interface and the result types.

    private sealed class DialogService : IDialogService
    {
        private readonly Window _owner;

        public DialogService(Window owner) => _owner = owner;

        public Task<bool> ConfirmDeleteAsync(string name)
            => new ConfirmDialog($"Delete \"{name}\"?\n\nThis cannot be undone.")
                   .ShowDialog<bool>(_owner);

        public Task<string?> PromptRenameAsync(string currentName)
            => new RenameDialog(currentName)
                   .ShowDialog<string?>(_owner);
    }
}
