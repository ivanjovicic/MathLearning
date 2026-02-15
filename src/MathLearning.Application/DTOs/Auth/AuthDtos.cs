namespace MathLearning.Application.DTOs.Auth;

public record LoginRequest(
    string Username,
    string Password
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
