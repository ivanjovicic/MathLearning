namespace MathLearning.Api.Startup;

public static class SeedAdminStartupPolicy
{
    public const string DevelopmentDefaultPassword = "UcimMatu!123";

    public sealed record Evaluation(
        bool ShouldSeed,
        bool ResetPasswordOnStart,
        string Username,
        string Email,
        string? Password,
        bool SuppressedPasswordReset,
        string? SkipReason);

    public static Evaluation Evaluate(IHostEnvironment environment, IConfiguration configuration)
    {
        var explicitlyEnabled = configuration.GetValue<bool>("SeedAdmin:Enabled");
        var enabled = environment.IsDevelopment() || explicitlyEnabled;

        var username = configuration["SeedAdmin:Username"] ?? "admin";
        var email = configuration["SeedAdmin:Email"] ?? "admin@mathlearning.com";

        if (!enabled)
        {
            return new Evaluation(
                ShouldSeed: false,
                ResetPasswordOnStart: false,
                Username: username,
                Email: email,
                Password: null,
                SuppressedPasswordReset: false,
                SkipReason: "SeedAdmin is disabled.");
        }

        var configuredPassword = configuration["SeedAdmin:Password"];
        var password = configuredPassword
            ?? (environment.IsDevelopment() ? DevelopmentDefaultPassword : null);

        if (string.IsNullOrWhiteSpace(password))
        {
            return new Evaluation(
                ShouldSeed: false,
                ResetPasswordOnStart: false,
                Username: username,
                Email: email,
                Password: null,
                SuppressedPasswordReset: false,
                SkipReason: "SeedAdmin enabled but `SeedAdmin__Password` is not configured.");
        }

        if (!environment.IsDevelopment()
            && string.Equals(password, DevelopmentDefaultPassword, StringComparison.Ordinal))
        {
            return new Evaluation(
                ShouldSeed: false,
                ResetPasswordOnStart: false,
                Username: username,
                Email: email,
                Password: null,
                SuppressedPasswordReset: false,
                SkipReason: "SeedAdmin refused the Development default password outside Development.");
        }

        var requestedReset = environment.IsDevelopment()
            || configuration.GetValue<bool>("SeedAdmin:ResetPasswordOnStart");

        var resetPasswordOnStart = requestedReset;
        var suppressedPasswordReset = false;

        if (!environment.IsDevelopment() && requestedReset)
        {
            var emergencyAllowed = configuration.GetValue<bool>("SeedAdmin:AllowEmergencyPasswordReset");
            if (!emergencyAllowed)
            {
                resetPasswordOnStart = false;
                suppressedPasswordReset = true;
            }
        }

        return new Evaluation(
            ShouldSeed: true,
            ResetPasswordOnStart: resetPasswordOnStart,
            Username: username,
            Email: email,
            Password: password,
            SuppressedPasswordReset: suppressedPasswordReset,
            SkipReason: null);
    }
}
