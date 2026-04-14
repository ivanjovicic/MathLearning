using System.Security.Claims;
using Hangfire;
using MathLearning.Application.DTOs.AntiCheat;
using MathLearning.Application.Services;
using MathLearning.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathLearning.Api.Controllers;

[ApiController]
[Authorize(Policy = DesignTokenSecurity.AdminPolicy)]
[Route("api/admin/anti-cheat")]
public sealed class AdminAntiCheatController : ControllerBase
{
    private readonly IAntiCheatAdminService antiCheatAdminService;
    private readonly IBackgroundJobClient backgroundJobs;

    public AdminAntiCheatController(
        IAntiCheatAdminService antiCheatAdminService,
        IBackgroundJobClient backgroundJobs)
    {
        this.antiCheatAdminService = antiCheatAdminService;
        this.backgroundJobs = backgroundJobs;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
        => Ok(await antiCheatAdminService.GetOverviewAsync(cancellationToken));

    [HttpGet("detections")]
    public async Task<IActionResult> Detections(
        [FromQuery] int take = 100,
        [FromQuery] string? reviewStatus = null,
        [FromQuery] string? severity = null,
        CancellationToken cancellationToken = default)
        => Ok(await antiCheatAdminService.GetDetectionsAsync(take, reviewStatus, severity, cancellationToken));

    [HttpPost("detections/{id:guid}/review")]
    public async Task<IActionResult> Review(
        Guid id,
        [FromBody] ReviewAntiCheatDetectionRequest request,
        CancellationToken cancellationToken)
        => Ok(await antiCheatAdminService.ReviewDetectionAsync(
            id,
            request.ReviewStatus,
            request.Notes,
            GetActorUserId(),
            cancellationToken));

    [HttpPost("detections/{id:guid}/ml-review")]
    public IActionResult TriggerMlReview(Guid id)
    {
        var jobId = backgroundJobs.Enqueue<IAntiCheatHangfireJobs>(x => x.RunMlReviewForDetectionJob(id));
        return Ok(new { enqueued = true, jobId, detectionId = id });
    }

    [HttpPost("ml-review/run")]
    public IActionResult RunMlReviewSweep([FromQuery] int take = 25)
    {
        var jobId = backgroundJobs.Enqueue<IAntiCheatHangfireJobs>(x => x.RunMlReviewSweepJob(take));
        return Ok(new { enqueued = true, jobId, take });
    }

    private string? GetActorUserId() =>
        User.FindFirstValue("userId") ??
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
