using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
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
        DataContext = new MainWindowViewModel(new DialogService(this));

        // Wire up chat panel behaviour after DataContext is set.
        WireChatPanel();
    }

    // ── File list double-tap ───────────────────────────────────────────────────

    private void OnFileDoubleTapped(object? sender, TappedEventArgs e)
        => ViewModel.OpenCommand.Execute(null);

    private void OnSearchResultDoubleTapped(object? sender, TappedEventArgs e)
        => ViewModel.OpenSearchResultCommand.Execute(null);

    // ── Chat panel wiring ─────────────────────────────────────────────────────

    private void WireChatPanel()
    {
        var grid         = this.FindControl<Grid>("MainContentGrid")!;
        var scrollViewer = this.FindControl<ScrollViewer>("ChatScrollViewer")!;
        var chat         = ViewModel.Chat;

        // Collapse the splitter (col 3) and panel (col 4) to zero width on startup
        // since IsVisible starts false, then keep them in sync with IsVisible.
        ApplyChatColumnWidths(grid, chat.IsVisible);
        chat.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ChatViewModel.IsVisible))
                ApplyChatColumnWidths(grid, chat.IsVisible);
        };

        // Auto-scroll to the newest message whenever the list changes.
        // Post at default priority so the new item has been measured/laid out
        // before we try to scroll to it.
        chat.Messages.CollectionChanged += (_, _) =>
            Dispatcher.UIThread.Post(() => scrollViewer.ScrollToEnd());
    }

    /// <summary>
    /// Sets the widths of the chat splitter (column 3) and chat panel (column 4)
    /// to their real values when the panel is open, or to 0 when it is closed.
    /// Mutating ColumnDefinition.Width directly is the reliable way to
    /// programmatically show/hide Grid columns in Avalonia.
    /// </summary>
    private static void ApplyChatColumnWidths(Grid grid, bool isVisible)
    {
        grid.ColumnDefinitions[3].Width = isVisible
            ? new GridLength(4)   : GridLength.Auto;   // splitter
        grid.ColumnDefinitions[4].Width = isVisible
            ? new GridLength(280) : GridLength.Auto;   // panel
    }

    // ── IDialogService implementation ──────────────────────────────────────────

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
