using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using AIFileExplorer.Models;

namespace AIFileExplorer.Services;

/// <summary>
/// Sends a full conversation history to Claude and returns the assistant's reply.
/// The caller is responsible for maintaining the history list.
/// </summary>
public class ChatService
{
    private const string SystemPrompt =
        """
        You are a helpful assistant embedded in an AI-powered file explorer application.
        You can help users find files, understand search results, and manage their filesystem.
        Keep responses concise. Use plain text — no markdown formatting in your replies.
        """;

    private readonly AnthropicClient _client;

    public ChatService() : this(new AnthropicClient()) { }

    public ChatService(AnthropicClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Sends <paramref name="history"/> to Claude and returns the next assistant message.
    /// The history must end with a user message.
    /// </summary>
    public async Task<string> SendAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken ct = default)
    {
        var messages = history
            .Select(m => new MessageParam
            {
                Role    = m.Role == ChatRole.User ? Role.User : Role.Assistant,
                Content = m.Text,
            })
            .ToList();

        var response = await _client.Messages.Create(
            new MessageCreateParams
            {
                Model     = Model.ClaudeHaiku4_5,
                MaxTokens = 1024,
                System    = SystemPrompt,
                Messages  = messages,
            },
            ct);

        return response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text ?? string.Empty;
    }
}
