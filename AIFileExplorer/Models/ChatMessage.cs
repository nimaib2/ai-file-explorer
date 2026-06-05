using System.ComponentModel;

namespace AIFileExplorer.Models;

public enum ChatRole { User, Assistant }

/// <summary>
/// A single message in the chat conversation.
/// Text implements INotifyPropertyChanged so the streaming path can append
/// tokens to an already-displayed bubble and have the binding update live.
/// </summary>
public class ChatMessage : INotifyPropertyChanged
{
    public required ChatRole Role { get; init; }

    private string _text = string.Empty;

    /// <summary>
    /// The message body. Settable so streaming can append tokens in-place.
    /// </summary>
    public required string Text
    {
        get => _text;
        set
        {
            if (_text == value) return;
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    /// <summary>
    /// When set, this text is sent to the Claude API instead of <see cref="Text"/>.
    /// Lets the UI show a short label ("Summarize: file.txt") while Claude receives
    /// the full file content.
    /// </summary>
    public string? OverrideApiText { get; init; }

    /// <summary>Convenience flag used by XAML to pick bubble side and colour.</summary>
    public bool IsUser => Role == ChatRole.User;

    public event PropertyChangedEventHandler? PropertyChanged;
}
