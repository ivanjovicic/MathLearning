using MathLearning.Application.DTOs.AntiCheat;
using MathLearning.Application.Services;

namespace MathLearning.Tests.Helpers;

public sealed class NoOpAnswerPatternAntiCheatService : IAnswerPatternAntiCheatService
{
    public Task<AntiCheatDetectionResultDto> EvaluateAndTrackAsync(
        AntiCheatAnswerObservationInput input,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new AntiCheatDetectionResultDto(
            false,
            0,
            string.Empty,
            string.Empty,
            "No anti-cheat evaluation performed in test stub.",
            [],
            new AntiCheatMlPromptDto("test", string.Empty, string.Empty, "{}")));

    public Task<IReadOnlyList<AntiCheatDetectionResultDto>> EvaluateAndTrackBatchAsync(
        IReadOnlyList<AntiCheatAnswerObservationInput> inputs,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<AntiCheatDetectionResultDto>>(
            inputs.Select(_ => new AntiCheatDetectionResultDto(
                false,
                0,
                string.Empty,
                string.Empty,
                "No anti-cheat evaluation performed in test stub.",
                [],
                new AntiCheatMlPromptDto("test", string.Empty, string.Empty, "{}"))).ToList());
}
