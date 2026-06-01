using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using AIFileExplorer.Models;

namespace AIFileExplorer.Services;

/// <summary>
/// Converts a natural-language search query into a <see cref="SearchFilter"/>
/// by asking Claude to extract structured fields from the query.
/// </summary>
public class NlSearchService
{
    // Kept as a constant so tests can assert against the exact contract Claude is given.
    public const string SystemPrompt =
        """
        You are a file-search query parser. The user will describe what files they are
        looking for in plain English. Your job is to extract structured fields from the
        query and return ONLY a JSON object — no explanation, no markdown, no code fences.

        Output schema (all fields are optional; omit or set to null if not mentioned):
        {
          "file_types":    string[]   // file extensions, e.g. [".pdf", ".docx"]
          "name_keywords": string[]   // keywords that should appear in the filename
          "date_from":     string     // ISO 8601 date, e.g. "2024-01-01"
          "date_to":       string     // ISO 8601 date
          "size_min":      string     // human size, e.g. "10MB", "500KB"
          "size_max":      string     // human size
          "location_hint": string     // folder name, e.g. "Downloads", "Documents", "Desktop"
        }

        Date rules (resolve all relative phrases using today's date):
        - "last month"      → date_from: first day of previous month, date_to: last day of previous month
        - "this month"      → date_from: first day of current month, date_to: today
        - "this week"       → date_from: Monday of current week, date_to: today
        - "last week"       → date_from: Monday of previous week, date_to: Sunday of previous week
        - "this year"       → date_from: January 1 of current year, date_to: today
        - "before 2024"     → date_to: "2023-12-31" (no date_from)
        - "in 2024"         → date_from: "2024-01-01", date_to: "2024-12-31"
        - "after June 2024" → date_from: "2024-07-01" (no date_to)
        - "last year"       → date_from: January 1 of previous year, date_to: December 31 of previous year

        Size rules:
        - "large files"             → size_min: "50MB"
        - "small files"             → size_max: "1MB"
        - "bigger/larger than X"    → size_min: X
        - "smaller/less than X"     → size_max: X

        Location rules:
        - "in Downloads" / "in the Downloads folder" → location_hint: "Downloads"
        - "on the Desktop"                           → location_hint: "Desktop"
        - "in Documents"                             → location_hint: "Documents"
        - "in Pictures"                              → location_hint: "Pictures"

        Common file categories (always set file_types when the user mentions these):
        - "videos" / "video files" / "movies"      → [".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v", ".flv"]
        - "photos" / "images" / "pictures"         → [".jpg", ".jpeg", ".png", ".heic", ".gif", ".webp", ".bmp", ".tiff"]
        - "documents" / "docs" (generic)           → [".pdf", ".docx", ".doc", ".txt", ".pages", ".rtf", ".odt"]
        - "spreadsheets"                           → [".xlsx", ".xls", ".csv", ".numbers"]
        - "presentations" / "slides"               → [".pptx", ".ppt", ".key"]
        - "audio" / "music" / "songs"              → [".mp3", ".m4a", ".wav", ".flac", ".aac"]
        - "archives" / "zip files"                 → [".zip", ".tar", ".gz", ".rar", ".7z"]
        - "code" / "scripts" / "source files"      → [".py", ".js", ".ts", ".cs", ".java", ".go", ".rs", ".cpp", ".c", ".sh"]

        Named document types (set both name_keywords AND file_types):
        - "resume" / "CV"  → name_keywords: ["resume"], file_types: [".pdf", ".docx", ".doc", ".pages"]
        - "invoice"        → name_keywords: ["invoice"], file_types: [".pdf", ".xlsx", ".docx"]
        - "receipt"        → name_keywords: ["receipt"], file_types: [".pdf", ".png", ".jpg"]
        - "screenshot"     → name_keywords: ["screenshot", "screen shot"], file_types: [".png", ".jpg", ".jpeg"]

        Return only the JSON object. Do not wrap it in a code block.
        """;

    // How long to wait for Claude before falling back to substring search.
    private const int ApiTimeoutMs = 5_000;

    // Maximum API attempts before giving up (on persistent 429 rate-limit errors).
    // Retries use exponential back-off: 1 s after attempt 0, 2 s after attempt 1.
    private const int MaxAttempts = 3;

    private readonly AnthropicClient _client;

    public NlSearchService() : this(new AnthropicClient()) { }

    public NlSearchService(AnthropicClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Sends <paramref name="query"/> to Claude and returns the parsed filter.
    /// <para>
    /// Robustness guarantees:
    /// <list type="bullet">
    ///   <item>Respects <paramref name="ct"/> — throws <see cref="OperationCanceledException"/>
    ///         if the caller cancels (e.g. the user started a new search).</item>
    ///   <item>5-second timeout — if Claude hasn't responded within 5 s, falls back to a
    ///         plain filename-substring filter built from the raw query words.</item>
    ///   <item>429 rate-limit retry — retries up to <see cref="MaxAttempts"/> times with
    ///         exponential back-off (1 s, 2 s) before falling back to substring search.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <returns>
    /// The parsed filter and a flag that is <c>true</c> when the fallback path was used
    /// (so the caller can show a degraded-mode status message).
    /// </returns>
    public async Task<(SearchFilter Filter, bool WasFallback)> ParseQueryAsync(
        string query, CancellationToken ct = default)
    {
        var system = SystemPrompt + $"\n\nToday's date is {DateTime.Today:yyyy-MM-dd}.";

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            // Link our per-attempt timeout to the caller's CancellationToken so that
            // (a) the user cancelling a superseded search still propagates, and
            // (b) we don't wait forever if Claude is slow.
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(ApiTimeoutMs);

            try
            {
                var response = await _client.Messages.Create(
                    new MessageCreateParams
                    {
                        Model     = Model.ClaudeHaiku4_5,
                        MaxTokens = 512,
                        System    = system,
                        Messages  = [new() { Role = Role.User, Content = query }],
                    },
                    timeoutCts.Token);

                var json = response.Content
                    .Select(b => b.Value)
                    .OfType<TextBlock>()
                    .FirstOrDefault()?.Text;

                return (SearchFilterParser.Parse(json), false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Our 5-second deadline fired (the caller did NOT cancel) — skip retries
                // and fall back immediately so the user gets results quickly.
                return (SubstringFallback(query), WasFallback: true);
            }
            // OperationCanceledException where ct.IsCancellationRequested is true
            // is NOT caught here — it propagates so the VM can ignore it cleanly.
            catch (Exception ex) when (IsRateLimitError(ex) && attempt < MaxAttempts - 1)
            {
                // 429 rate-limited — exponential back-off before the next attempt.
                // attempt 0 → wait 1 s, attempt 1 → wait 2 s
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
            // Any other exception on the last attempt propagates to the caller.
        }

        // All attempts were rate-limited — degrade gracefully.
        return (SubstringFallback(query), WasFallback: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a bare filter from the raw query words so the file engine can still
    /// do a substring search when Claude is unavailable.
    /// Words of 2 characters or fewer (articles, prepositions, etc.) are dropped.
    /// </summary>
    private static SearchFilter SubstringFallback(string query)
    {
        var words = query
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(w => w.Length > 2)
            .ToList();

        return new SearchFilter { NameKeywords = words.Count > 0 ? words : null };
    }

    /// <summary>
    /// Returns true when an exception looks like a 429 rate-limit response from the API.
    /// </summary>
    private static bool IsRateLimitError(Exception ex) =>
        ex.Message.Contains("429", StringComparison.Ordinal)                    ||
        ex.Message.Contains("rate limit",  StringComparison.OrdinalIgnoreCase)  ||
        ex.Message.Contains("rate_limit",  StringComparison.OrdinalIgnoreCase);
}
