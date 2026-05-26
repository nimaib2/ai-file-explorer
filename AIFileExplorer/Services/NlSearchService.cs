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
          "location_hint": string     // path fragment, e.g. "Downloads", "~/Documents"
        }

        Rules:
        - Relative dates like "last week" or "this month" must be resolved to ISO 8601
          based on today's date.
        - "large files" means size_min of "100MB".
        - "small files" means size_max of "1MB".
        - Return only the JSON object. Do not wrap it in a code block.
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
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model      = Model.ClaudeHaiku4_5,   // fast + cheap for structured extraction
            MaxTokens  = 512,
            System     = SystemPrompt,
            Messages   = [new() { Role = Role.User, Content = query }],
        });

        var json = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text;

        return SearchFilterParser.Parse(json);
    }
}
