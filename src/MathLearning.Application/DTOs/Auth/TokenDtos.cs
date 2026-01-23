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
