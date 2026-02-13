namespace MathLearning.Application.DTOs.Auth;

public record LoginRequest(
    string Username,
    string Password
);

public record LoginResponse(
    string Token,
    int UserId,
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
    int UserId,
    string Username,
    string Message
);
