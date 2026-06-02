using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AIFileExplorer.Models;
using AIFileExplorer.Services;

namespace AIFileExplorer.ViewModels;

public partial class ChatViewModel : ObservableObject
{
    private readonly ChatService _chatService = new();
    private CancellationTokenSource? _sendCts;

    /// <summary>All messages in the conversation, in chronological order.</summary>
    public ObservableCollection<ChatMessage> Messages { get; } = [];

    /// <summary>Text currently typed in the input box.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _inputText = string.Empty;

    /// <summary>Whether the chat panel is open.</summary>
    [ObservableProperty]
    private bool _isVisible;

    /// <summary>True while waiting for Claude's response.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private bool _isSending;

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

        // Cancel any previous in-flight send (shouldn't normally happen since the
        // button is disabled while sending, but guards against rapid keyboard use).
        _sendCts?.Cancel();
        _sendCts = new CancellationTokenSource();

        IsSending = true;
        try
        {
            // Snapshot the history so the API call isn't affected by future mutations.
            var history = Messages.ToList();
            var reply   = await _chatService.SendAsync(history, _sendCts.Token);
            Messages.Add(new ChatMessage { Role = ChatRole.Assistant, Text = reply });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer send — do nothing.
        }
        catch (Exception ex)
        {
            Messages.Add(new ChatMessage
            {
                Role = ChatRole.Assistant,
                Text = $"Error: {ex.Message}",
            });
        }
        finally
        {
            IsSending = false;
        }
    }

    private bool CanSend() => !IsSending && !string.IsNullOrWhiteSpace(InputText);
}
