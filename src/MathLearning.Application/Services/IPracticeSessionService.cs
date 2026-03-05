using MathLearning.Application.DTOs.Practice;

namespace MathLearning.Application.Services;

public interface IPracticeSessionService
{
    Task<StartPracticeSessionResponse> StartSessionAsync(
        string userId,
        StartPracticeSessionRequest request,
        CancellationToken ct = default);

    Task<SubmitPracticeAnswerResponse> SubmitAnswerAsync(
        string userId,
        Guid sessionId,
        SubmitPracticeAnswerRequest request,
        CancellationToken ct = default);

    Task<CompletePracticeSessionResponse> CompleteSessionAsync(
        string userId,
        Guid sessionId,
        CancellationToken ct = default);
}
