using Microsoft.AspNetCore.SignalR;
using TaxAdvisorBot.Application.Interfaces;

namespace TaxAdvisorBot.Web.Hubs;

/// <summary>
/// SignalR hub for real-time tax advisor chat.
/// Streams AI responses token-by-token to the connected client.
/// </summary>
public sealed class ChatHub : Hub
{
    private readonly IConversationService _conversationService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IConversationService conversationService, ILogger<ChatHub> logger)
    {
        _conversationService = conversationService;
        _logger = logger;
    }

    /// <summary>
    /// Receives a user message and streams the AI response back via SignalR.
    /// </summary>
    public async IAsyncEnumerable<string> SendMessage(string sessionId, string message)
    {
        _logger.LogInformation("ChatHub: session={SessionId}, message length={Length}", sessionId, message.Length);

        await foreach (var chunk in _conversationService.ChatAsync(sessionId, message))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Creates a new chat session and returns the session ID.
    /// </summary>
    public string CreateSession()
    {
        return Guid.NewGuid().ToString("N")[..8];
    }
}
