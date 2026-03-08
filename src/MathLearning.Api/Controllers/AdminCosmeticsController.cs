using System.Security.Claims;
using MathLearning.Application.DTOs.Cosmetics;
using MathLearning.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathLearning.Api.Controllers;

[ApiController]
[Authorize(Policy = DesignTokenSecurity.AdminPolicy)]
[Route("api/admin/cosmetics")]
public sealed class AdminCosmeticsController : ControllerBase
{
    private readonly ICosmeticAdminService adminService;

    public AdminCosmeticsController(ICosmeticAdminService adminService)
    {
        this.adminService = adminService;
    }

    [HttpGet("items")]
    public async Task<IActionResult> GetItems(CancellationToken cancellationToken)
        => Ok(await adminService.GetItemsAsync(cancellationToken));

    [HttpPost("items")]
    public async Task<IActionResult> CreateItem(
        [FromBody] UpsertCosmeticItemRequest request,
        CancellationToken cancellationToken)
        => Ok(await adminService.UpsertItemAsync(null, request, GetActorUserId(), cancellationToken));

    [HttpPut("items/{id:int}")]
    public async Task<IActionResult> UpdateItem(
        int id,
        [FromBody] UpsertCosmeticItemRequest request,
        CancellationToken cancellationToken)
        => Ok(await adminService.UpsertItemAsync(id, request, GetActorUserId(), cancellationToken));

    [HttpGet("reward-rules")]
    public async Task<IActionResult> GetRewardRules(CancellationToken cancellationToken)
        => Ok(await adminService.GetRewardRulesAsync(cancellationToken));

    [HttpPost("reward-rules")]
    public async Task<IActionResult> CreateRewardRule(
        [FromBody] UpsertCosmeticRewardRuleRequest request,
        CancellationToken cancellationToken)
        => Ok(await adminService.UpsertRewardRuleAsync(null, request, GetActorUserId(), cancellationToken));

    [HttpPut("reward-rules/{id:int}")]
    public async Task<IActionResult> UpdateRewardRule(
        int id,
        [FromBody] UpsertCosmeticRewardRuleRequest request,
        CancellationToken cancellationToken)
        => Ok(await adminService.UpsertRewardRuleAsync(id, request, GetActorUserId(), cancellationToken));

    [HttpGet("seasons")]
    public async Task<IActionResult> GetSeasons(CancellationToken cancellationToken)
        => Ok(await adminService.GetAdminSeasonsAsync(cancellationToken));

    [HttpPost("seasons")]
    public async Task<IActionResult> CreateSeason(
        [FromBody] UpsertCosmeticSeasonRequest request,
        CancellationToken cancellationToken)
        => Ok(await adminService.UpsertSeasonAsync(null, request, GetActorUserId(), cancellationToken));

    [HttpPut("seasons/{id:int}")]
    public async Task<IActionResult> UpdateSeason(
        int id,
        [FromBody] UpsertCosmeticSeasonRequest request,
        CancellationToken cancellationToken)
        => Ok(await adminService.UpsertSeasonAsync(id, request, GetActorUserId(), cancellationToken));

    [HttpPost("reward-tracks")]
    public async Task<IActionResult> CreateRewardTrackEntry(
        [FromBody] UpsertRewardTrackEntryRequest request,
        CancellationToken cancellationToken)
        => Ok(await adminService.UpsertRewardTrackAsync(null, request, GetActorUserId(), cancellationToken));

    [HttpPut("reward-tracks/{id:int}")]
    public async Task<IActionResult> UpdateRewardTrackEntry(
        int id,
        [FromBody] UpsertRewardTrackEntryRequest request,
        CancellationToken cancellationToken)
        => Ok(await adminService.UpsertRewardTrackAsync(id, request, GetActorUserId(), cancellationToken));

    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics(CancellationToken cancellationToken)
        => Ok(await adminService.GetAnalyticsSummaryAsync(cancellationToken));

    private string? GetActorUserId() =>
        User.FindFirstValue("userId") ??
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
