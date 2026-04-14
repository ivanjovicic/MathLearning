using System.Security.Claims;
using MathLearning.Application.DTOs.DesignTokens;
using MathLearning.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathLearning.Api.Controllers;

[ApiController]
[Authorize(Policy = DesignTokenSecurity.AdminPolicy)]
[Route("api/admin/tokens")]
public sealed class AdminTokensController : ControllerBase
{
    private readonly IDesignTokenAdminService adminService;

    public AdminTokensController(IDesignTokenAdminService adminService)
    {
        this.adminService = adminService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(await adminService.GetAdminTokensAsync(cancellationToken));

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] UpsertDesignTokensRequest request, CancellationToken cancellationToken)
        => Ok(await adminService.UpsertDraftAsync(request, GetActorUserId(), GetActorName(), HttpContext.TraceIdentifier, cancellationToken));

    [HttpPost("version")]
    public async Task<IActionResult> Publish([FromBody] PublishDesignTokenVersionRequest request, CancellationToken cancellationToken)
        => Ok(await adminService.PublishDraftAsync(request, GetActorUserId(), GetActorName(), HttpContext.TraceIdentifier, cancellationToken));

    [HttpPost("rollback/{version}")]
    public async Task<IActionResult> Rollback(
        string version,
        [FromBody] RollbackDesignTokenVersionRequest? request,
        CancellationToken cancellationToken)
        => Ok(await adminService.RollbackAsync(version, request ?? new RollbackDesignTokenVersionRequest(null, null), GetActorUserId(), GetActorName(), HttpContext.TraceIdentifier, cancellationToken));

    [HttpGet("history")]
    public async Task<IActionResult> History(CancellationToken cancellationToken)
        => Ok(await adminService.GetHistoryAsync(cancellationToken));

    private string? GetActorUserId() =>
        User.FindFirstValue("userId") ??
        User.FindFirstValue(ClaimTypes.NameIdentifier);

    private string? GetActorName() =>
        User.FindFirstValue(ClaimTypes.Name) ??
        User.Identity?.Name;
}
