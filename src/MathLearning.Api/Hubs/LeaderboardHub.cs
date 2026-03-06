using Microsoft.AspNetCore.SignalR;
using MathLearning.Core.DTOs;

namespace MathLearning.Api.Hubs;

public class LeaderboardHub : Hub
{
    private readonly IHubContext<LeaderboardHub> _hubContext;
    private readonly ILogger<LeaderboardHub> _logger;

    public LeaderboardHub(IHubContext<LeaderboardHub> hubContext, ILogger<LeaderboardHub> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task SendRankUpdate(string userId, RankUpdateNotification notification)
    {
        await _hubContext.Clients.User(userId).SendAsync("RankUpdate", notification);
        _logger.LogInformation("Sent rank update to user {UserId}: {Notification}", userId, notification);
    }
}