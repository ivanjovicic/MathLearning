using System.Net;
using System.Net.Http.Json;
using MathLearning.Api;
using MathLearning.Application.Services;
using MathLearning.Application.DTOs.Leaderboard;
using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Leaderboard;
using MathLearning.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MathLearning.Tests.Endpoints;

public sealed class SchoolLeaderboardReadMutationHttpTests : IClassFixture<RealSchoolLeaderboardWebApplicationFactory>
{
    private readonly RealSchoolLeaderboardWebApplicationFactory factory;
    private readonly HttpClient client;

    public SchoolLeaderboardReadMutationHttpTests(RealSchoolLeaderboardWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task SchoolLeaderboardRoutes_DoNotWriteAndExposeStaleMetadata()
    {
        await SeedSchoolAsync();

        using var beforeScope = factory.Services.CreateScope();
        var beforeDb = beforeScope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var beforeAggregateCount = await beforeDb.SchoolScoreAggregates.CountAsync();
        var beforeHistoryCount = await beforeDb.SchoolRankHistories.CountAsync();

        using var listResponse = await SendGetAsync("/api/leaderboard/schools?period=week&limit=5");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<SchoolLeaderboardResponseDto>();
        Assert.NotNull(list);
        Assert.Equal("week", list!.Period);
        Assert.True(list.IsStale);
        Assert.True(list.GeneratedAtUtc > DateTime.MinValue);

        using var detailResponse = await SendGetAsync("/api/leaderboard/schools/77?period=week&neighbors=2");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<SchoolLeaderboardDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal("week", detail!.Period);

        using var historyResponse = await SendGetAsync("/api/leaderboard/schools/history/77?period=week&take=10");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
        var history = await historyResponse.Content.ReadFromJsonAsync<SchoolLeaderboardHistoryResponseDto>();
        Assert.NotNull(history);
        Assert.Equal(77, history!.SchoolId);
        Assert.Empty(history.Points);

        using var afterScope = factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<ApiDbContext>();
        Assert.Equal(beforeAggregateCount, await afterDb.SchoolScoreAggregates.CountAsync());
        Assert.Equal(beforeHistoryCount, await afterDb.SchoolRankHistories.CountAsync());
    }

    private async Task SeedSchoolAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var periodInfo = SchoolLeaderboardPeriods.Normalize("week");

        if (!await db.Schools.AnyAsync(x => x.Id == 77))
        {
            db.Schools.Add(new School { Id = 77, Name = "Read School" });
        }

        var profile = await db.UserProfiles.SingleAsync(x => x.UserId == "1");
        profile.SchoolId = 77;
        profile.LeaderboardOptIn = true;
        profile.UpdatedAt = DateTime.UtcNow;

        if (!await db.SchoolScoreAggregates.AnyAsync(x =>
                x.SchoolId == 77 &&
                x.Period == periodInfo.Period &&
                x.PeriodStartUtc == periodInfo.PeriodStartUtc))
        {
            db.SchoolScoreAggregates.Add(new SchoolScoreAggregate
            {
                SchoolId = 77,
                Period = periodInfo.Period,
                PeriodStartUtc = periodInfo.PeriodStartUtc,
                XpTotal = 500,
                ActiveStudents = 1,
                EligibleStudents = 1,
                AverageXpPerActiveStudent = 500m,
                ParticipationRate = 1m,
                CompositeScore = 99m,
                Rank = 1,
                UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-10)
            });
        }

        await db.SaveChangesAsync();
    }

    private async Task<HttpResponseMessage> SendGetAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("X-Test-UserId", "1");
        return await client.SendAsync(request);
    }
}

public sealed class RealSchoolLeaderboardWebApplicationFactory : CustomWebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILeaderboardService>();
            services.RemoveAll<ISchoolLeaderboardService>();
            services.AddScoped<LeaderboardService>();
            services.AddScoped<ILeaderboardService>(sp => sp.GetRequiredService<LeaderboardService>());
            services.AddScoped<ISchoolLeaderboardService>(sp => sp.GetRequiredService<LeaderboardService>());
        });
    }
}
