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

    // ── File list double-tap ───────────────────────────────────────────────────
    // Converts the Avalonia TappedEventArgs UI event into a ViewModel command.
    // DoubleTapped has no native command binding in Avalonia without a behavior
    // package, so this one-line bridge stays in code-behind.

    private void OnFileDoubleTapped(object? sender, TappedEventArgs e)
        => ViewModel.OpenCommand.Execute(null);

    private void OnSearchResultDoubleTapped(object? sender, TappedEventArgs e)
        => ViewModel.OpenSearchResultCommand.Execute(null);

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
