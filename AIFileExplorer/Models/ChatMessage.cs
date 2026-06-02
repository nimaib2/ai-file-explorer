namespace AIFileExplorer.Models;

public enum ChatRole { User, Assistant }

public class ChatMessage
{
    public required ChatRole Role { get; init; }
    public required string   Text { get; init; }

    // Convenience flag used by the XAML DataTemplate to pick bubble side + colour.
    public bool IsUser => Role == ChatRole.User;
}
