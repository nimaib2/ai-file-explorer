using System;
using System.Linq;
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

        Return only the JSON object. Do not wrap it in a code block.
        """;

    private readonly AnthropicClient _client;

    public NlSearchService() : this(new AnthropicClient()) { }

    public NlSearchService(AnthropicClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Sends <paramref name="query"/> to Claude and returns the parsed filter.
    /// Falls back to an empty filter on any error.
    /// </summary>
    public async Task<SearchFilter> ParseQueryAsync(string query)
    {
        // Append today's date so Claude can resolve relative phrases like
        // "last week" or "this month" to concrete ISO 8601 dates.
        var system = SystemPrompt + $"\n\nToday's date is {DateTime.Today:yyyy-MM-dd}.";

        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model      = Model.ClaudeHaiku4_5,   // fast + cheap for structured extraction
            MaxTokens  = 512,
            System     = system,
            Messages   = [new() { Role = Role.User, Content = query }],
        });

        var json = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text;

        return SearchFilterParser.Parse(json);
    }
}
