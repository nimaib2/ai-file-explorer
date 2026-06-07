using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AIFileExplorer.Models;
using AIFileExplorer.Services;
using AIFileExplorer.ViewModels;
using AIFileExplorer.Views;

namespace AIFileExplorer;

/// <summary>
/// Code-behind for the main application window. Handles UI concerns that cannot
/// be expressed in MVVM bindings alone: column-width animation for the chat panel,
/// auto-scroll for the streaming chat view, and dialog injection into the ViewModel.
/// </summary>
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

        // Auto-scroll to the newest message whenever the list changes, and also
        // as streaming tokens arrive (they update Text, not the collection).
        chat.Messages.CollectionChanged += (_, e) =>
        {
            Dispatcher.UIThread.Post(() => scrollViewer.ScrollToEnd(), DispatcherPriority.Background);

            // Subscribe to property changes on any new assistant message so the
            // view scrolls to the bottom as each streaming token extends the bubble.
            if (e.NewItems is null) return;
            foreach (ChatMessage msg in e.NewItems)
            {
                if (msg.IsUser) continue;
                msg.PropertyChanged += (_, _) =>
                    Dispatcher.UIThread.Post(() => scrollViewer.ScrollToEnd(), DispatcherPriority.Background);
            }
        };
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
            ? new GridLength(4)   : new GridLength(0);   // splitter
        grid.ColumnDefinitions[4].Width = isVisible
            ? new GridLength(280) : new GridLength(0);   // panel
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
