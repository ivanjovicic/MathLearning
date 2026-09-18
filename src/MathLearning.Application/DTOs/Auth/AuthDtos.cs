namespace MathLearning.Application.DTOs.Auth;

public record LoginRequest(
    string Username,
    string Password
);

public record AuthFailureResponse(
    string Code,
    string Message,
    string? CorrelationId = null,
    int? RetryAfterSeconds = null
);

public record PasswordResetForgotRequest(string Email);

public record PasswordResetRequest(
    string Email,
    string Token,
    string NewPassword
);

public record PasswordResetResponse(
    bool Success,
    string Code,
    string Message
);

public record LoginResponse(
    string Token,
    string UserId,
    string Username
);

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string? SchoolName = null,
    string? FacultyName = null
);

public record RegisterResponse(
    string UserId,
    string Username,
    string Message
);
