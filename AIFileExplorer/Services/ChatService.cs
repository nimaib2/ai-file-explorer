using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Anthropic;
using Anthropic.Models.Messages;
using AIFileExplorer.Models;

namespace AIFileExplorer.Services;

/// <summary>
/// Streams a Claude response token-by-token for a full conversation history.
/// The caller owns the history list and is responsible for appending the
/// returned tokens to a new assistant message after the stream completes.
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
    /// Sends <paramref name="history"/> to Claude via the streaming API and yields
    /// each text token as it arrives. The history must end with a user message.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only <see cref="RawContentBlockDeltaEvent"/> events carrying a
    /// <see cref="TextDelta"/> are yielded; all other stream events (message start,
    /// content-block start/stop, message stop) are silently skipped.
    /// </para>
    /// </remarks>
    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> history,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var messages = history
            .Select(m => new MessageParam
            {
                Role    = m.Role == ChatRole.User ? Role.User : Role.Assistant,
                Content = m.Text,
            })
            .ToList();

        var stream = _client.Messages.CreateStreaming(
            new MessageCreateParams
            {
                Model     = Model.ClaudeHaiku4_5,
                MaxTokens = 1024,
                System    = SystemPrompt,
                Messages  = messages,
            },
            ct);

        await foreach (var evt in stream)
        {
            // We only care about text deltas; skip start, stop, and non-text deltas.
            if (evt.TryPickContentBlockDelta(out var blockDelta) &&
                blockDelta.Delta.TryPickText(out var textDelta) &&
                !string.IsNullOrEmpty(textDelta.Text))
            {
                yield return textDelta.Text;
            }
        }
    }
}
