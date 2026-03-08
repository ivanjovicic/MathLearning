using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
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
        [FromServices] IValidator<UpsertCosmeticItemRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        return Ok(await adminService.UpsertItemAsync(null, request, GetActorUserId(), cancellationToken));
    }

    [HttpPut("items/{id:int}")]
    public async Task<IActionResult> UpdateItem(
        int id,
        [FromBody] UpsertCosmeticItemRequest request,
        [FromServices] IValidator<UpsertCosmeticItemRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        return Ok(await adminService.UpsertItemAsync(id, request, GetActorUserId(), cancellationToken));
    }

    [HttpPost("items/{id:int}/release")]
    public async Task<IActionResult> ReleaseItem(int id, CancellationToken cancellationToken)
        => Ok(await adminService.ReleaseItemAsync(id, GetActorUserId(), cancellationToken));

    [HttpPost("items/{id:int}/retire")]
    public async Task<IActionResult> RetireItem(int id, CancellationToken cancellationToken)
        => Ok(await adminService.RetireItemAsync(id, GetActorUserId(), cancellationToken));

    [HttpGet("reward-rules")]
    public async Task<IActionResult> GetRewardRules(CancellationToken cancellationToken)
        => Ok(await adminService.GetRewardRulesAsync(cancellationToken));

    [HttpPost("reward-rules")]
    public async Task<IActionResult> CreateRewardRule(
        [FromBody] UpsertCosmeticRewardRuleRequest request,
        [FromServices] IValidator<UpsertCosmeticRewardRuleRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        return Ok(await adminService.UpsertRewardRuleAsync(null, request, GetActorUserId(), cancellationToken));
    }

    [HttpPut("reward-rules/{id:int}")]
    public async Task<IActionResult> UpdateRewardRule(
        int id,
        [FromBody] UpsertCosmeticRewardRuleRequest request,
        [FromServices] IValidator<UpsertCosmeticRewardRuleRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        return Ok(await adminService.UpsertRewardRuleAsync(id, request, GetActorUserId(), cancellationToken));
    }

    [HttpGet("seasons")]
    public async Task<IActionResult> GetSeasons(CancellationToken cancellationToken)
        => Ok(await adminService.GetAdminSeasonsAsync(cancellationToken));

    [HttpPost("seasons")]
    public async Task<IActionResult> CreateSeason(
        [FromBody] UpsertCosmeticSeasonRequest request,
        [FromServices] IValidator<UpsertCosmeticSeasonRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        return Ok(await adminService.UpsertSeasonAsync(null, request, GetActorUserId(), cancellationToken));
    }

    [HttpPut("seasons/{id:int}")]
    public async Task<IActionResult> UpdateSeason(
        int id,
        [FromBody] UpsertCosmeticSeasonRequest request,
        [FromServices] IValidator<UpsertCosmeticSeasonRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        return Ok(await adminService.UpsertSeasonAsync(id, request, GetActorUserId(), cancellationToken));
    }

    [HttpPost("seasons/{id:int}/activate")]
    public async Task<IActionResult> ActivateSeason(int id, CancellationToken cancellationToken)
        => Ok(await adminService.ActivateSeasonAsync(id, GetActorUserId(), cancellationToken));

    [HttpPost("seasons/{id:int}/archive")]
    public async Task<IActionResult> ArchiveSeason(int id, CancellationToken cancellationToken)
        => Ok(await adminService.ArchiveSeasonAsync(id, GetActorUserId(), cancellationToken));

    [HttpPost("reward-tracks")]
    public async Task<IActionResult> CreateRewardTrackEntry(
        [FromBody] UpsertRewardTrackEntryRequest request,
        [FromServices] IValidator<UpsertRewardTrackEntryRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        return Ok(await adminService.UpsertRewardTrackAsync(null, request, GetActorUserId(), cancellationToken));
    }

    [HttpPut("reward-tracks/{id:int}")]
    public async Task<IActionResult> UpdateRewardTrackEntry(
        int id,
        [FromBody] UpsertRewardTrackEntryRequest request,
        [FromServices] IValidator<UpsertRewardTrackEntryRequest> validator,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return ValidationProblem(validation);
        }

        return Ok(await adminService.UpsertRewardTrackAsync(id, request, GetActorUserId(), cancellationToken));
    }

    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics(CancellationToken cancellationToken)
        => Ok(await adminService.GetAnalyticsSummaryAsync(cancellationToken));

    private string? GetActorUserId() =>
        User.FindFirstValue("userId") ??
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private ActionResult ValidationProblem(ValidationResult validation)
    {
        foreach (var error in validation.Errors)
        {
            ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return base.ValidationProblem(ModelState);
    }
}
