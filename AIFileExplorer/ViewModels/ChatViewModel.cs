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

public partial class ChatViewModel : ObservableObject
{
    private readonly ChatService _chatService = new();
    private CancellationTokenSource? _sendCts;

    /// <summary>
    /// Full conversation history in chronological order.
    /// Both user and assistant messages live here; the streaming path mutates
    /// the last assistant message in-place via ChatMessage.Text (observable).
    /// </summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    /// <summary>Whether the chat panel is open.</summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>
    /// True from the moment Send is invoked until the entire stream finishes.
    /// Disables the send button throughout so the user cannot start a second
    /// request while one is in flight.
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

    /// <summary>
    /// True only while waiting for the first streaming token.
    /// Drives the "Claude is thinking…" indicator — it disappears the moment
    /// text starts arriving, replaced by the growing response bubble.
    /// </summary>
    [ObservableProperty]
    private bool _isThinking;

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
        _sendCts = new CancellationTokenSource();
        var ct = _sendCts.Token;

        IsSending  = true;
        IsThinking = true;

        // Add an empty assistant bubble that streaming will fill in word-by-word.
        var assistantMsg = new ChatMessage { Role = ChatRole.Assistant, Text = string.Empty };
        Messages.Add(assistantMsg);

        try
        {
            // History = everything except the empty placeholder we just added.
            // The stream adds text to the placeholder directly via the mutable Text property.
            var history = Messages.Take(Messages.Count - 1).ToList();

            await foreach (var chunk in _chatService.StreamAsync(history, ct))
            {
                if (IsThinking)
                {
                    // First token arrived — hide the "thinking" indicator.
                    await Dispatcher.UIThread.InvokeAsync(() => IsThinking = false);
                }

                // Append the token to the live bubble on the UI thread so
                // ChatMessage.PropertyChanged fires on the correct thread.
                await Dispatcher.UIThread.InvokeAsync(() => assistantMsg.Text += chunk);
            }
        }
        catch (OperationCanceledException)
        {
            // A new send superseded this one — leave the partial bubble as-is.
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
}
