using System.Security.Claims;
using MathLearning.Application.DTOs.Sync;
using MathLearning.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathLearning.Api.Controllers;

[ApiController]
[Authorize(Policy = DesignTokenSecurity.AdminPolicy)]
[Route("api/admin/sync")]
public sealed class AdminSyncController : ControllerBase
{
    private readonly ISyncAdminService syncAdminService;

    public AdminSyncController(ISyncAdminService syncAdminService)
    {
        this.syncAdminService = syncAdminService;
    }

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken cancellationToken)
        => Ok(await syncAdminService.GetOverviewAsync(cancellationToken));

    [HttpGet("devices")]
    public async Task<IActionResult> Devices([FromQuery] int take = 100, CancellationToken cancellationToken = default)
        => Ok(await syncAdminService.GetDevicesAsync(take, cancellationToken));

    [HttpGet("dead-letters")]
    public async Task<IActionResult> DeadLetters(
        [FromQuery] int take = 100,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
        => Ok(await syncAdminService.GetDeadLettersAsync(take, status, cancellationToken));

    [HttpPost("dead-letters/{operationId:guid}/redrive")]
    public async Task<IActionResult> Redrive(
        Guid operationId,
        CancellationToken cancellationToken)
        => Ok(await syncAdminService.RedriveDeadLetterAsync(operationId, GetActorUserId(), cancellationToken));

    [HttpPost("dead-letters/redrive")]
    public async Task<IActionResult> RedriveBatch(
        [FromBody] RedriveSyncDeadLettersRequest? request,
        CancellationToken cancellationToken)
    {
        var effectiveRequest = request ?? new RedriveSyncDeadLettersRequest();
        return Ok(await syncAdminService.RedriveDeadLettersAsync(
            effectiveRequest.Take,
            effectiveRequest.IncludeExhausted,
            GetActorUserId(),
            cancellationToken));
    }

    private string? GetActorUserId() =>
        User.FindFirstValue("userId") ??
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
