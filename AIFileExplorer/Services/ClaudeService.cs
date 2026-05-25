using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;

namespace AIFileExplorer.Services;

public class ClaudeService
{
    private readonly AnthropicClient _client;

    public ClaudeService()
    {
        _client = new AnthropicClient();
    }

    public async Task SayHelloAsync()
    {
        var response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = Model.ClaudeOpus4_6,
            MaxTokens = 1024,
            Messages = [new() { Role = Role.User, Content = "Hello Claude!" }]
        });

        var text = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .FirstOrDefault()?.Text ?? string.Empty;

        Debug.WriteLine($"[ClaudeService] Response: {text}");
    }
}
