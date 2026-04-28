using Microsoft.AspNetCore.SignalR;
using TaxAdvisorBot.Application.Interfaces;
using TaxAdvisorBot.Domain.Models;
using TaxAdvisorBot.Web.Hubs;

namespace TaxAdvisorBot.Web.Services;

/// <summary>
/// Pushes progress updates and completions to web clients via SignalR.
/// </summary>
public sealed class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<ChatHub> _hubContext;

    public SignalRNotificationService(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task SendProgressAsync(string sessionId, ProgressUpdate update, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveProgress", sessionId, update, cancellationToken);
    }

    public async Task SendCompletionAsync(string sessionId, ChatResponse response, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.All.SendAsync("ReceiveCompletion", sessionId, response, cancellationToken);
    }
}
