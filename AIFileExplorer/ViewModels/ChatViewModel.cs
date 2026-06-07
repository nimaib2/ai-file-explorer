using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIFileExplorer.Models;
using AIFileExplorer.Services;

namespace AIFileExplorer.ViewModels;

/// <summary>
/// ViewModel for the collapsible AI chat sidebar.
/// Owns the conversation history, the input box state, and the streaming
/// send/summarize flows. A <see cref="Func{FileContext}"/> delegate is injected
/// at construction so the VM can snapshot the current explorer state at the
/// moment a message is sent without holding a direct reference to
/// <see cref="MainWindowViewModel"/>.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private readonly ChatService    _chatService = new();
    private readonly Func<FileContext> _getContext;
    private CancellationTokenSource? _sendCts;

    /// <param name="getContext">
    /// Called on each send to capture the current explorer state.
    /// Using a delegate instead of a direct reference keeps the ViewModel
    /// decoupled and ensures we always get a fresh snapshot, not a stale one.
    /// </param>
    public ChatViewModel(Func<FileContext> getContext)
    {
        _getContext = getContext;
    }

    /// <summary>Full conversation history in chronological order.</summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    /// <summary>Whether the chat panel is open.</summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>
    /// True from the moment Send is invoked until the entire stream finishes.
    /// Keeps the send button disabled so the user cannot start a second request
    /// while one is already in flight.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    /// <summary>
    /// True only while waiting for the very first streaming token.
    /// Drives the "Claude is thinking…" indicator; cleared the moment text arrives.
    /// </summary>
    [ObservableProperty]
    private bool _isThinking;

    /// <summary>
    /// One-line summary of what Claude currently knows about the explorer state.
    /// Set by MainWindowViewModel whenever the path, selection, or search results change.
    /// Displayed in the chat panel context bar so the user can see what context is active.
    /// </summary>
    [ObservableProperty]
    private string _contextSummary = string.Empty;

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleVisible() => IsVisible = !IsVisible;

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task Send()
    {
        var text = InputText.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        InputText = string.Empty;
        Messages.Add(new ChatMessage { Role = ChatRole.User, Text = text });

        _sendCts?.Cancel();
        _sendCts?.Dispose();
        _sendCts = new CancellationTokenSource();
        var ct = _sendCts.Token;

        // Capture context at send-time, not at construction time.
        var context = _getContext();

        IsSending  = true;
        IsThinking = true;

        var assistantMsg = new ChatMessage { Role = ChatRole.Assistant, Text = string.Empty };
        Messages.Add(assistantMsg);

        try
        {
            // Exclude the empty placeholder from the history sent to Claude.
            var history = Messages.Take(Messages.Count - 1).ToList();

            await foreach (var chunk in _chatService.StreamAsync(history, context, ct))
            {
                if (IsThinking)
                    await Dispatcher.UIThread.InvokeAsync(() => IsThinking = false);

                await Dispatcher.UIThread.InvokeAsync(() => assistantMsg.Text += chunk);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer send — leave the partial bubble as-is.
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => assistantMsg.Text = $"Error: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsThinking = false;
                IsSending  = false;
            });
        }
    }

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(InputText);

    // ── Summarize ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens the chat panel and streams a Claude summary for the provided file content.
    /// The UI bubble shows "Summarize: {displayName}" while Claude receives the full text.
    /// </summary>
    public async Task SummarizeAsync(string displayName, string content, bool wasTruncated)
    {
        IsVisible = true;   // open the panel if it's closed

        _sendCts?.Cancel();
        _sendCts?.Dispose();
        _sendCts = new CancellationTokenSource();
        var ct = _sendCts.Token;

        var context = _getContext();

        var apiPrompt = wasTruncated
            ? $"Please summarize the following file ({displayName}). Note: only the first 100 KB is shown.\n\n{content}"
            : $"Please summarize the following file ({displayName}):\n\n{content}";

        var userMsg = new ChatMessage
        {
            Role            = ChatRole.User,
            Text            = $"Summarize: {displayName}",
            OverrideApiText = apiPrompt,
        };
        Messages.Add(userMsg);

        IsSending  = true;
        IsThinking = true;

        var assistantMsg = new ChatMessage { Role = ChatRole.Assistant, Text = string.Empty };
        Messages.Add(assistantMsg);

        try
        {
            var history = Messages.Take(Messages.Count - 1).ToList();
            await foreach (var chunk in _chatService.StreamAsync(history, context, ct))
            {
                if (IsThinking)
                    await Dispatcher.UIThread.InvokeAsync(() => IsThinking = false);
                await Dispatcher.UIThread.InvokeAsync(() => assistantMsg.Text += chunk);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => assistantMsg.Text = $"Error: {ex.Message}");
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsThinking = false;
                IsSending  = false;
            });
        }
    }
}
