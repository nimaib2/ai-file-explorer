using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Anthropic;
using Anthropic.Models.Messages;
using AIFileExplorer.Models;

namespace AIFileExplorer.Services;

/// <summary>
/// Streams a Claude response token-by-token for a full conversation history.
/// A <see cref="FileContext"/> snapshot is baked into the system prompt on every
/// call so Claude always sees what the user is currently looking at.
/// </summary>
public class ChatService
{
    private readonly AnthropicClient _client;

    public ChatService() : this(new AnthropicClient()) { }

    public ChatService(AnthropicClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Sends <paramref name="history"/> (ending with a user message) to Claude via the
    /// streaming API and yields each text token as it arrives.
    /// The <paramref name="context"/> snapshot is embedded in the system prompt so Claude
    /// can answer questions about the current directory and selection.
    /// </summary>
    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> history,
        FileContext context,
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
                System    = BuildSystemPrompt(context),
                Messages  = messages,
            },
            ct);

        await foreach (var evt in stream)
        {
            if (evt.TryPickContentBlockDelta(out var blockDelta) &&
                blockDelta.Delta.TryPickText(out var textDelta) &&
                !string.IsNullOrEmpty(textDelta.Text))
            {
                yield return textDelta.Text;
            }
        }
    }

    // ── System prompt ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a system prompt that grounds Claude in the user's current explorer state.
    /// The prompt is constructed fresh on every call so it always reflects the latest
    /// directory, selection, and search results — not a stale snapshot.
    /// </summary>
    private static string BuildSystemPrompt(FileContext ctx)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            "You are a helpful assistant embedded in an AI-powered file explorer application.");
        sb.AppendLine(
            "Answer questions about the user's files and filesystem. Be concise.");
        sb.AppendLine(
            "Use plain text only — no markdown, no bullet symbols, no code fences.");

        // ── Current directory ──────────────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine($"Current directory: {ctx.CurrentPath}");

        // ── Selected file ──────────────────────────────────────────────────────
        if (ctx.SelectedEntry is { } sel)
        {
            sb.AppendLine();
            sb.AppendLine(
                $"You have selected: {sel.Name} " +
                $"({sel.DisplaySize}, modified {sel.LastModified:MMM d, yyyy})");
            sb.AppendLine($"Full path: {sel.FullPath}");
        }

        // ── Search results or directory listing ────────────────────────────────
        if (ctx.IsShowingSearchResults && ctx.SearchQuery is { Length: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine($"The user searched for: \"{ctx.SearchQuery}\"");
            sb.AppendLine($"Search returned {ctx.SearchResults.Count} result(s):");
            foreach (var f in ctx.SearchResults.Take(40))
                sb.AppendLine(
                    $"  {f.Name}  ({f.DisplaySize}, {f.LastModified:MM/dd/yy})  {f.FullPath}");
            if (ctx.SearchResults.Count > 40)
                sb.AppendLine($"  … and {ctx.SearchResults.Count - 40} more");
        }
        else
        {
            var dirs  = ctx.DirectoryEntries.Where(e =>  e.IsDirectory).ToList();
            var files = ctx.DirectoryEntries.Where(e => !e.IsDirectory)
                            .OrderByDescending(e => e.SizeBytes)   // largest first so size questions are always correct
                            .ToList();
            sb.AppendLine();
            sb.AppendLine($"Directory contains {dirs.Count} folder(s) and {files.Count} file(s):");

            foreach (var e in dirs)
                sb.AppendLine($"  {e.Name}  (folder)");

            const int fileLimit = 200;
            foreach (var e in files.Take(fileLimit))
                sb.AppendLine($"  {e.Name}  ({e.DisplaySize}, {e.LastModified:MM/dd/yy})");

            if (files.Count > fileLimit)
                sb.AppendLine($"  … and {files.Count - fileLimit} smaller files not shown");
        }

        return sb.ToString().TrimEnd();
    }
}
