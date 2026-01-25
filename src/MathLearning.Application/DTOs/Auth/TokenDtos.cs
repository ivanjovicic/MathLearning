namespace MathLearning.Application.DTOs.Auth;

public record TokenRequest(
    string RefreshToken
);

public record TokenResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    int UserId,
    string Username
);

public record RevokeTokenRequest(
    string RefreshToken
);

// 📱 Mobile Registration DTOs
public record MobileRegisterRequest(
    string Username,
    string Email,
    string Password,
    string? DisplayName = null
);

public record MobileRegisterResponse(
    bool Success,
    string Message,
    TokenResponse? Tokens = null,
    UserProfileDto? Profile = null
);

public record UserProfileDto(
    int UserId,
    string Username,
    string? DisplayName,
    int Coins,
    int Level,
    int Xp,
    int Streak,
    DateTime CreatedAt
);
