using MathLearning.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace MathLearning.Api.Controllers;

[ApiController]
[Route("api/ui/tokens")]
public sealed class UiTokensController : ControllerBase
{
    private readonly IDesignTokenQueryService queryService;

    public UiTokensController(IDesignTokenQueryService queryService)
    {
        this.queryService = queryService;
    }

    [HttpGet]
    public Task<IActionResult> GetTokens([FromQuery] string? theme, CancellationToken cancellationToken)
        => Execute(() => queryService.GetCurrentTokensAsync(theme, cancellationToken));

    [HttpGet("version")]
    public Task<IActionResult> GetVersion(CancellationToken cancellationToken)
        => Execute(() => queryService.GetCurrentVersionAsync(cancellationToken));

    [HttpGet("theme/{theme}")]
    public Task<IActionResult> GetTheme(string theme, CancellationToken cancellationToken)
        => Execute(() => queryService.GetCurrentTokensByThemeAsync(theme, cancellationToken));

    private async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
