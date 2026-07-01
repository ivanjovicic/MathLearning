using MathLearning.Application.DTOs.Cosmetics;

namespace MathLearning.Application.DTOs.Users;

public record PublicUserSearchResultDto(
    string UserId,
    string DisplayName,
    int Level,
    string? AvatarUrl = null,
    AvatarAppearanceDto? Appearance = null
);

public record PublicUserProfileDto(
    string UserId,
    string DisplayName,
    int Level,
    int Streak,
    string? AvatarUrl = null,
    AvatarAppearanceDto? Appearance = null
);
