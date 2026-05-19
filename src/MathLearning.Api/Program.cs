using MathLearning.Api.Endpoints;
using MathLearning.Api.Middleware;
using MathLearning.Api.Services;
using MathLearning.Api.Startup;
using MathLearning.Application.Validators;
using MathLearning.Application.Services;
using MathLearning.Core.Services;
using FluentValidation;
using MathLearning.Domain.Events;
using MathLearning.Infrastructure;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.Leaderboard;
using MathLearning.Infrastructure.Services.EventBus;
using MathLearning.Infrastructure.Services.EventBus.Handlers;
using MathLearning.Infrastructure.Services.Performance;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using Serilog.Formatting.Json;
using StackExchange.Redis;
using System.Data.Common;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Text;

// ?? Configure Serilog EARLY (before builder)
var startupEnvironment =
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? "Production";

var isDevelopment = string.Equals(startupEnvironment, "Development", StringComparison.OrdinalIgnoreCase);

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Is(isDevelopment ? LogEventLevel.Information : LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    // TEMPORARY TRIAGE: increase EF Core log level in Development to capture SQL/params
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", isDevelopment ? LogEventLevel.Information : LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithEnvironmentName()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId();

if (isDevelopment)
{
    loggerConfig.WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

    // Local dev convenience (Fly logs use stdout/stderr anyway)
    loggerConfig.WriteTo.File(
        path: "logs/mathlearning-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
}
else
{
    // Fly-friendly structured logs
    loggerConfig.WriteTo.Console(new JsonFormatter());
}

Log.Logger = loggerConfig.CreateLogger();

try
{
    Log.Information("?? Starting MathLearning API");
    Log.Information(
        "Startup environment detected: Environment={Environment} ASPNETCORE_URLS={AspNetCoreUrls} PORT={Port}",
        startupEnvironment,
        Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "<not-set>",
        Environment.GetEnvironmentVariable("PORT") ?? "<not-set>");

    var builder = WebApplication.CreateBuilder(args);

    // ?? Use Serilog for logging
    builder.Host.UseSerilog();

    // Fly.io / container safety: some platforms set PORT instead of ASPNETCORE_URLS.
    // Only apply when PORT is explicitly provided so local dev keeps its default ports.
    var portEnv = builder.Configuration["PORT"];
    if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        Log.Information("?? Binding to PORT from environment: {Port}", port);
    }
    else
    {
        var configuredUrls = builder.Configuration["ASPNETCORE_URLS"];
        if (isDevelopment)
        {
            var resolved = ResolveDevelopmentUrlsWithAutoPortFallback(configuredUrls);
            if (!string.IsNullOrWhiteSpace(resolved.Urls))
            {
                builder.WebHost.UseUrls(resolved.Urls);
            }

            if (resolved.PortFallbacks.Count > 0)
            {
                Log.Warning(
                    "?? Auto-selected fallback URL(s) due to occupied port(s). Original={OriginalUrls} Effective={EffectiveUrls} Fallbacks={Fallbacks}",
                    resolved.OriginalUrls ?? "<default:http://localhost:5000>",
                    resolved.Urls,
                    resolved.PortFallbacks);
            }
            else
            {
                Log.Information("?? Using development URLs: {Urls}", resolved.Urls);
            }
        }
        else
        {
            Log.Information("?? Using launch/profile URLs (no explicit PORT override).");
        }
    }

    var defaultConnectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(defaultConnectionString) && builder.Environment.IsEnvironment("Test"))
    {
        defaultConnectionString = "Host=localhost;Port=5433;Username=postgres;Password=postgres;Database=mathlearning_test_bootstrap;";
    }

    if (string.IsNullOrWhiteSpace(defaultConnectionString))
    {
        Log.Error("? ConnectionStrings:Default is not configured. Set deployment env var `ConnectionStrings__Default` before deploying.");
        throw new InvalidOperationException("Missing ConnectionStrings:Default. Configure ConnectionStrings__Default.");
    }
    else
    {
        Log.Information("??? DB target ({Environment}): {DbTarget}", builder.Environment.EnvironmentName, DescribeDbConnection(defaultConnectionString));
    }

    if (!builder.Environment.IsDevelopment() &&
        !builder.Environment.IsEnvironment("Test") &&
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Default")) &&
        ConnectionStringTargetsLoopback(defaultConnectionString))
    {
        Log.Error(
            "? ConnectionStrings:Default resolved to a loopback/local fallback in {Environment}: {DbTarget}. Configure deployment env var `ConnectionStrings__Default`.",
            builder.Environment.EnvironmentName,
            DescribeDbConnection(defaultConnectionString));
        throw new InvalidOperationException(
            "Production deployment is using a local fallback database connection string. Set ConnectionStrings__Default.");
    }

    builder.AddObservabilityServices();
    builder.AddDatabaseServices(defaultConnectionString, isDevelopment);
    var backgroundJobRuntimeState = new BackgroundJobRuntimeState();
    builder.Services.AddSingleton(backgroundJobRuntimeState);
    var hangfireEnabled = await builder.AddBackgroundJobServices(
        defaultConnectionString,
        (connectionString, cancellationToken) => CanOpenPostgresConnectionAsync(connectionString, cancellationToken));
    backgroundJobRuntimeState.HangfireEnabled = hangfireEnabled;
    backgroundJobRuntimeState.DisabledReason = hangfireEnabled
        ? null
        : builder.Environment.IsEnvironment("Test")
            ? "Disabled in test environment."
            : "PostgreSQL unavailable at startup.";
    builder.AddApplicationLayerServices();
    builder.AddCacheAndInfrastructureServices();

    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var fallbackJwtSecret = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
    var jwtSecret = jwtSettings["SecretKey"];
    var isDevelopmentOrTest = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test");

    if (string.IsNullOrWhiteSpace(jwtSecret))
    {
        if (isDevelopmentOrTest)
        {
            jwtSecret = fallbackJwtSecret;
        }
        else
        {
            Log.Error("JwtSettings:SecretKey is missing in {Environment}.", builder.Environment.EnvironmentName);
            throw new InvalidOperationException("Missing JwtSettings:SecretKey. Configure a production secret.");
        }
    }

    if (!string.IsNullOrWhiteSpace(jwtSecret) &&
        jwtSecret.Length < 32)
    {
        Log.Error(
            "JwtSettings:SecretKey must be at least 32 characters in {Environment}.",
            builder.Environment.EnvironmentName);
        throw new InvalidOperationException("Invalid JwtSettings:SecretKey. Secret must be at least 32 characters long.");
    }

    if (!isDevelopmentOrTest &&
        string.Equals(jwtSecret, fallbackJwtSecret, StringComparison.Ordinal))
    {
        Log.Error(
            "JwtSettings:SecretKey is using the fallback value in {Environment}.",
            builder.Environment.EnvironmentName);
        throw new InvalidOperationException("Invalid JwtSettings:SecretKey. Configure a non-fallback production secret.");
    }

    builder.AddSecurityServices();
    builder.AddApiDocumentationServices();

    var app = builder.Build();

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Log.Information("?? Listening on: {Urls}", string.Join(", ", app.Urls));
    });

    // ?? Global exception handler (production-safe problem+json)
    app.UseMiddleware<MathLearning.Api.Middleware.GlobalExceptionMiddleware>();

    // ?? Correlation ID (must be BEFORE request logging so it appears on the request completion log)
    app.UseMiddleware<MathLearning.Api.Middleware.CorrelationIdMiddleware>();

    // ?? Add Serilog request logging
    app.UseMiddleware<RequestPerformanceLoggingMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
            diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
            diagnosticContext.Set("XForwardedFor", httpContext.Request.Headers["X-Forwarded-For"].ToString());
            diagnosticContext.Set("CorrelationId", httpContext.Response.Headers[MathLearning.Api.Middleware.CorrelationIdMiddleware.HeaderName].ToString());
        };
    });

    var databaseStartupSucceeded = false;
    var userProfileIdentitySchemaReady = false;
    var schemaState = app.Services.GetRequiredService<DatabaseSchemaState>();
    var databaseStartupMode = DatabaseSchemaVersionGuard.ResolveStartupMode(app.Environment, app.Configuration);

    // Database startup is explicit by environment: Development may auto-migrate, higher environments must already be aligned.
    if (!EF.IsDesignTime && databaseStartupMode != DatabaseStartupMode.Skip)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
        var schemaGuard = scope.ServiceProvider.GetRequiredService<DatabaseSchemaVersionGuard>();

        try
        {
            Log.Information("Database startup mode resolved to {StartupMode}.", databaseStartupMode);

            if (databaseStartupMode == DatabaseStartupMode.AutoMigrate)
            {
                var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
                if (pending.Length > 0)
                {
                    Log.Information("Applying pending database migrations: {Migrations}", string.Join(", ", pending));
                }
                else
                {
                    Log.Information("No pending database migrations.");
                }

                await db.Database.MigrateAsync();
                Log.Information("Database migrations applied successfully.");
            }
            else
            {
                Log.Information(
                    "Startup migrations are disabled in {Environment}. Validating exact database schema alignment instead.",
                    app.Environment.EnvironmentName);
            }

            var schemaStatus = await schemaGuard.CheckAsync(db);
            schemaState.Update(schemaStatus);

            if (!schemaStatus.IsSchemaReady)
            {
                throw schemaGuard.CreateMismatchException(databaseStartupMode, app.Environment.EnvironmentName, schemaStatus);
            }

            databaseStartupSucceeded = true;

            await CosmeticStartupSeeder.EnsureCosmeticItemsAsync(scope.ServiceProvider, CancellationToken.None);

            userProfileIdentitySchemaReady = await HasUserProfileIdentitySchemaAsync(db);

            var designTokenQueryService = scope.ServiceProvider.GetRequiredService<IDesignTokenQueryService>();
            await designTokenQueryService.EnsureInitializedAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (IsPostgresAuthFailure(ex))
            {
                Log.Error(
                    ex,
                    "Database startup failed because PostgreSQL rejected the configured credentials for {DbTarget}. Update deployment env var `ConnectionStrings__Default` with the current database password.",
                    DescribeDbConnection(defaultConnectionString));
            }

            schemaState.Update(DatabaseSchemaStatus.Failed(
                failureMessage: ex is InvalidOperationException
                    ? ex.Message
                    : $"Database startup failed in {app.Environment.EnvironmentName}. StartupMode={databaseStartupMode}. See logs for details.",
                latestCodeMigration: schemaState.Current.LatestCodeMigration,
                latestAppliedMigration: schemaState.Current.LatestAppliedMigration,
                pendingMigrations: schemaState.Current.PendingMigrations,
                unknownAppliedMigrations: schemaState.Current.UnknownAppliedMigrations));

            Log.Error(ex, "Database startup failed. StartupMode={StartupMode}", databaseStartupMode);
            throw;
        }

        var contentSeedEnabled = app.Configuration.GetValue<bool>("SeedContent:Enabled");

        if (databaseStartupSucceeded && contentSeedEnabled)
        {
            var seedOptions = new DbContextOptionsBuilder<ApiDbContext>()
                .UseNpgsql(
                    defaultConnectionString,
                    npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .Options;

            await using var seedDb = new ApiDbContext(seedOptions);

            try
            {
                Log.Information("?? Seeding database...");
                await DbSeeder.SeedAsync(seedDb);
                Log.Information("? Database seeding complete");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "?? Database seeding failed after successful migrations. Continuing startup without seed data.");
            }
        }
        else if (databaseStartupSucceeded)
        {
            Log.Information("Content seeding skipped. Set `SeedContent__Enabled=true` to run DbSeeder on startup.");
        }
    }

    // Seed default admin user (only in Development or when explicitly enabled)
    try
    {
        var seedAdminEnabled =
            app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("SeedAdmin:Enabled");

        if (seedAdminEnabled)
        {
            if (!databaseStartupSucceeded)
            {
                Log.Warning("Skipping admin seeding because database startup did not complete successfully.");
            }
            else if (!userProfileIdentitySchemaReady)
            {
                Log.Warning("Skipping admin seeding because UserProfiles.UserId is not compatible with Identity text keys.");
            }
            else
            {
                await SeedAdminUser(app);
            }
        }
        else
        {
            Log.Warning("SeedAdmin is disabled (set `SeedAdmin__Enabled=true` to enable). Skipping admin seeding.");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Admin seeding failed, continuing without seeding.");
    }

    // Seed deterministic Development/Test login accounts for local app testing.
    try
    {
        var shouldSeedTestAccounts = app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Test");
        if (!shouldSeedTestAccounts)
        {
            Log.Information(
                "Skipping Development/Test account seeding in environment {Environment}.",
                app.Environment.EnvironmentName);
        }
        else if (!databaseStartupSucceeded)
        {
            Log.Warning("Skipping Development/Test account seeding because database startup did not complete successfully.");
        }
        else if (!userProfileIdentitySchemaReady)
        {
            Log.Warning("Skipping Development/Test account seeding because UserProfiles.UserId is not compatible with Identity text keys.");
        }
        else
        {
            using var seedScope = app.Services.CreateScope();
            var testAccountSeeder = ActivatorUtilities.CreateInstance<TestAccountSeeder>(seedScope.ServiceProvider);
            await testAccountSeeder.SeedAsync(app.Environment);
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Development/Test account seeding failed, continuing startup.");
    }

    // Configure the HTTP request pipeline.
    // Fly terminates TLS at the edge and forwards to the app over HTTP.
    // Forwarded headers are required so ASP.NET Core can correctly detect scheme/host and avoid HTTPS redirect loops.
    var forwardedHeadersOptions = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
    };
    forwardedHeadersOptions.KnownNetworks.Clear();
    forwardedHeadersOptions.KnownProxies.Clear();
    app.UseForwardedHeaders(forwardedHeadersOptions);

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        
        // Don't force HTTPS redirect in Development to avoid CORS issues
        Log.Information("?? HTTPS redirection disabled in Development mode");
    }
    else
    {
        // Only use HTTPS redirection in Production
        app.UseHttpsRedirection();
    }

    // Enable CORS
    app.UseCors();

    // Sliding-window in-memory rate-limiter (single-node)
    app.UseMiddleware<MathLearning.Api.Middleware.InMemorySlidingWindowRateLimitMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

    // Health endpoint for Fly.io / uptime checks
    app.MapHealthChecks("/health");
    app.MapHealthEndpoints();
    IResult BuildBackgroundJobsHealthResponse(BackgroundJobRuntimeState backgroundJobState)
    {
        var payload = new
        {
            hangfireEnabled = backgroundJobState.HangfireEnabled,
            disabledReason = backgroundJobState.DisabledReason,
        };

        if (backgroundJobState.HangfireEnabled)
        {
            return Results.Ok(payload);
        }

        return Results.Json(
            payload,
            statusCode: builder.Environment.IsEnvironment("Test")
                ? StatusCodes.Status200OK
                : StatusCodes.Status503ServiceUnavailable);
    }

    app.MapGet("/health/background-jobs", BuildBackgroundJobsHealthResponse)
    .AllowAnonymous()
    .WithName("BackgroundJobsHealth")
    .WithDescription("Reports whether background jobs were enabled at startup");

    app.MapGet("/api/health/background-jobs", BuildBackgroundJobsHealthResponse)
    .AllowAnonymous()
    .WithName("BackgroundJobsHealthApi")
    .WithDescription("Reports whether background jobs were enabled at startup");

    // Minimal runtime metrics (no Prometheus dependency)
    app.MapGet("/metrics", () =>
    {
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

        return Results.Json(new
        {
            uptimeSeconds = (long)uptime.TotalSeconds,
            memoryMb = process.WorkingSet64 / 1024 / 1024,
            gcTotalMemoryMb = GC.GetTotalMemory(forceFullCollection: false) / 1024 / 1024,
            threadCount = process.Threads.Count,
            timestampUtc = DateTime.UtcNow,
        });
    });

    // Map Auth endpoints (no auth required)
    app.MapAuthEndpoints();
    app.MapControllers();

    // Map User endpoints
    app.MapUserEndpoints();

    // Map Quiz endpoints
    app.MapQuizEndpoints();
    app.MapSrsEndpoints();

    // Map question authoring endpoints
    app.MapQuestionAuthoringEndpoints();

    // Map offline sync endpoints
    app.MapSyncEndpoints();

    // Map Adaptive endpoints
    app.MapAdaptiveEndpoints();

    // Map practice session endpoints
    app.MapPracticeSessionEndpoints();

    // Map analytics/recommendations endpoints
    app.MapAnalyticsEndpoints();

    // Map explanation endpoints
    app.MapExplanationEndpoints();

    // Map Hint endpoints
    app.MapHintEndpoints();

    // Map Coin endpoints
    app.MapCoinEndpoints();

    // Map Powerup endpoints
    app.MapPowerupEndpoints();
    app.MapEconomySettlementEndpoints();

    // Map Progress endpoints
    app.MapProgressEndpoints();
    // Map Daily Run endpoints
    app.MapDailyRunEndpoints();

    // Map Leaderboard endpoints
    app.MapLeaderboardEndpoints();

    // Map Avatar/Cosmetic endpoints
    app.MapAvatarEndpoints();

    // Map Maintenance endpoints
    app.MapMaintenanceEndpoints();


    // Monitoring endpoints (mock, for admin UI)
    app.MapGet("/api/monitoring/jobs", () =>
    {
        // TODO: Replace with real job status from Hangfire or background services
        var now = DateTime.UtcNow;
            return Results.Json(new[]
        {
            new { Name = "XP Daily Reset", IsSuccess = true, LastMessage = "Zadnji reset uspešan", Timestamp = now.AddMinutes(-30) },
            new { Name = "Leaderboard Sync", IsSuccess = true, LastMessage = "Leaderboard ažuriran", Timestamp = now.AddMinutes(-10) },
            new { Name = "Hangfire Worker", IsSuccess = true, LastMessage = "Svi jobovi OK", Timestamp = now.AddMinutes(-1) }
        });
    });


    app.MapGet("/api/monitoring/logs", () =>
    {
        // Cita poslednjih 20 linija iz Serilog log fajla (ako postoji)
        var logPath = Path.Combine(AppContext.BaseDirectory, "Logs", "log.txt");
        if (!System.IO.File.Exists(logPath))
            return Results.Json(new[] { "Log fajl nije pronaden: " + logPath });
        var lines = System.IO.File.ReadLines(logPath).Reverse().Take(20).Reverse().ToList();
        return Results.Json(lines);
    });

    app.MapGet("/api/monitoring/logs-advanced", (string? search, string? level) =>
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "Logs", "log.txt");
        if (!System.IO.File.Exists(logPath))
            return Results.Json(new[] { new { Message = "Log fajl nije pronaden: " + logPath } });

        var lines = System.IO.File.ReadLines(logPath).Reverse().Take(200).Reverse();
        var entries = new List<object>();
        foreach (var line in lines)
        {
            var msg = line;
            string? lvl = null;
            string? stack = null;
            var idx1 = line.IndexOf('[');
            var idx2 = line.IndexOf(']');
            if (idx1 >= 0 && idx2 > idx1)
                lvl = line.Substring(idx1 + 1, idx2 - idx1 - 1);
            if (!string.IsNullOrEmpty(level) && !string.Equals(lvl, level, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrEmpty(search) && !line.Contains(search, StringComparison.OrdinalIgnoreCase))
                continue;
            entries.Add(new { Message = msg, Level = lvl, StackTrace = stack });
        }

        return Results.Json(entries);
    });

    // Map Logging endpoints
    app.MapLoggingEndpoints();

    if (app.Environment.IsDevelopment() && hangfireEnabled)
    {
        app.UseHangfireDashboard("/hangfire");
    }

    // Map Bug endpoints
    app.MapBugEndpoints();

    // Serve uploaded avatars
    var uploadsPath = Path.Combine(app.Environment.ContentRootPath, "uploads");
    Directory.CreateDirectory(uploadsPath);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });

    app.MapGet("/", () => Results.Ok("MathLearning API is running"));

    Log.Information("? MathLearning API started successfully");

    if (!app.Environment.IsEnvironment("Test") && databaseStartupSucceeded && hangfireEnabled)
    {
        var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<IPracticeHangfireJobs>(
            "practice-daily-aggregation",
            job => job.DailyAggregationJob(),
            "0 2 * * *");
        recurringJobManager.AddOrUpdate<ISchoolLeaderboardHangfireJobs>(
            "school-leaderboard-refresh",
            job => job.RefreshAllCurrentPeriodsJob(),
            "*/10 * * * *");
        recurringJobManager.AddOrUpdate<ISchoolLeaderboardHangfireJobs>(
            "school-leaderboard-weekly-snapshot",
            job => job.CaptureSnapshotJob("week"),
            "0 * * * *");
        recurringJobManager.AddOrUpdate<ISchoolLeaderboardHangfireJobs>(
            "school-leaderboard-monthly-snapshot",
            job => job.CaptureSnapshotJob("month"),
            "15 */6 * * *");
        recurringJobManager.AddOrUpdate<IAntiCheatHangfireJobs>(
            "anti-cheat-ml-review-sweep",
            job => job.RunMlReviewSweepJob(0),
            "*/5 * * * *");
    }
    else if (!app.Environment.IsEnvironment("Test") && !hangfireEnabled)
    {
        Log.Warning("Skipping Hangfire recurring job registration because Hangfire is disabled for this process.");
    }
    else if (!app.Environment.IsEnvironment("Test"))
    {
        Log.Warning("Skipping Hangfire recurring job registration because database startup did not complete successfully.");
    }
    
    app.Run();
}
catch (Microsoft.Extensions.Hosting.HostAbortedException)
{
    // Expected when EF Core tooling uses the startup project to build the host and then aborts it.
}
catch (Exception ex)
{
    if (IsAddressInUse(ex))
    {
        var conflictPort = TryExtractPortFromAddressInUse(ex);
        Log.Error(
            "? Port binding failed because the address is already in use. Port={Port}. Stop the process on that port or run with a different URL, e.g. ASPNETCORE_URLS=http://localhost:5180",
            conflictPort);
        LogAddressInUseDiagnostics(conflictPort);
    }

    Log.Fatal(ex, "? Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Seed admin user method
static async Task SeedAdminUser(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

    if (!await roleManager.RoleExistsAsync(DesignTokenSecurity.AdminRole))
    {
        await roleManager.CreateAsync(new IdentityRole(DesignTokenSecurity.AdminRole));
    }

    static async Task EnsurePasswordAsync(UserManager<IdentityUser> userManager, IdentityUser user, string password)
    {
        if (await userManager.HasPasswordAsync(user))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, resetToken, password);
            if (!resetResult.Succeeded)
            {
                Log.Warning($"? Failed to reset password for '{user.UserName}': {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            var addResult = await userManager.AddPasswordAsync(user, password);
            if (!addResult.Succeeded)
            {
                Log.Warning($"? Failed to add password for '{user.UserName}': {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
            }
        }
    }
    
    // ?? Seed Admin User
    var adminUsername = app.Configuration["SeedAdmin:Username"] ?? "admin";
    var adminPassword =
        app.Configuration["SeedAdmin:Password"]
        ?? (app.Environment.IsDevelopment() ? "UcimMatu!123" : null);

    var resetAdminPasswordOnStart =
        app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("SeedAdmin:ResetPasswordOnStart");

    if (string.IsNullOrWhiteSpace(adminPassword))
    {
        Log.Warning("SeedAdmin enabled but `SeedAdmin__Password` not set. Skipping admin seeding.");
        return;
    }

    var existingAdmin = await userManager.FindByNameAsync(adminUsername);
    
    if (existingAdmin == null)
    {
        var admin = new IdentityUser
        {
            UserName = adminUsername,
            Email = app.Configuration["SeedAdmin:Email"] ?? "admin@mathlearning.com",
            EmailConfirmed = true
        };
        
        var result = await userManager.CreateAsync(admin, adminPassword);
        
        if (result.Succeeded)
        {
            Log.Information("? Admin user created successfully!");
            await userManager.AddToRoleAsync(admin, DesignTokenSecurity.AdminRole);
            
            // Create UserProfile for admin (UserId matches Identity key)
            string adminUserId = admin.Id;
             
            if (!await db.UserProfiles.AnyAsync(p => p.UserId == adminUserId))
            {
                var adminProfile = new MathLearning.Domain.Entities.UserProfile
                {
                    UserId = adminUserId,
                    Username = adminUsername,
                    DisplayName = "Admin",
                    Coins = 500,
                    Level = 5,
                    Xp = 420,
                    Streak = 7,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.UserProfiles.Add(adminProfile);
                await db.SaveChangesAsync();
                Log.Information("? Admin user profile created!");
            }
        }
        else
        {
            Log.Warning($"? Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
    else
    {
        Log.Information("? Admin user already exists.");
        if (!await userManager.IsInRoleAsync(existingAdmin, DesignTokenSecurity.AdminRole))
        {
            await userManager.AddToRoleAsync(existingAdmin, DesignTokenSecurity.AdminRole);
        }
        if (resetAdminPasswordOnStart)
        {
            await EnsurePasswordAsync(userManager, existingAdmin, adminPassword);
            Log.Information("? Admin password ensured on startup (SeedAdmin:ResetPasswordOnStart).");
        }
    }
}

static string DescribeDbConnection(string connectionString)
{
    try
    {
        var csb = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        static string Get(DbConnectionStringBuilder builder, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (builder.TryGetValue(key, out var value) && value is not null)
                {
                    var text = value.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }

            return "<n/a>";
        }

        var host = Get(csb, "Host", "Server", "Data Source");
        var port = Get(csb, "Port");
        var database = Get(csb, "Database", "Initial Catalog");
        var user = Get(csb, "Username", "User ID", "UserId", "UID");
        var sslMode = Get(csb, "SSL Mode", "Ssl Mode");
        var channelBinding = Get(csb, "Channel Binding");
        var pooling = Get(csb, "Pooling");

        return $"Host={host};Port={port};Database={database};User={user};SSL Mode={sslMode};Channel Binding={channelBinding};Pooling={pooling}";
    }
    catch
    {
        return "<unparseable connection string>";
    }
}

static bool ConnectionStringTargetsLoopback(string connectionString)
{
    try
    {
        var csb = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString
        };

        foreach (var key in new[] { "Host", "Server", "Data Source" })
        {
            if (csb.TryGetValue(key, out var value) && value is not null)
            {
                var host = value.ToString();
                if (!string.IsNullOrWhiteSpace(host))
                    return IsLoopbackHost(host);
            }
        }
    }
    catch
    {
    }

    return false;
}

static async Task<bool> CanOpenPostgresConnectionAsync(string connectionString, CancellationToken ct = default)
{
    try
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = 5,
            CommandTimeout = 5
        };

        await using var connection = new NpgsqlConnection(csb.ConnectionString);
        await connection.OpenAsync(ct);
        return true;
    }
    catch (Exception ex)
    {
        if (IsPostgresAuthFailure(ex))
        {
            Log.Error(
                ex,
                "Initial PostgreSQL connectivity probe failed because credentials were rejected for {DbTarget}. Update deployment env var `ConnectionStrings__Default` with the correct password.",
                DescribeDbConnection(connectionString));
            return false;
        }

        Log.Warning(ex, "Initial PostgreSQL connectivity probe failed for {DbTarget}", DescribeDbConnection(connectionString));
        return false;
    }
}

static bool IsPostgresAuthFailure(Exception ex)
{
    for (Exception? current = ex; current != null; current = current.InnerException)
    {
        if (current is PostgresException postgresException &&
            string.Equals(postgresException.SqlState, PostgresErrorCodes.InvalidPassword, StringComparison.Ordinal))
        {
            return true;
        }
    }

    return false;
}

static async Task<bool> HasUserProfileIdentitySchemaAsync(ApiDbContext db, CancellationToken ct = default)
{
    var connectionString = db.Database.GetConnectionString();
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        return false;
    }

    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync(ct);

    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = current_schema()
              AND table_name = 'UserProfiles'
              AND column_name = 'UserId'
              AND udt_name = 'text'
        );
        """;

    var result = await command.ExecuteScalarAsync(ct);
    return result is bool boolResult && boolResult;
}

static bool IsAddressInUse(Exception ex)
{
    for (Exception? current = ex; current != null; current = current.InnerException)
    {
        if (current is SocketException socketException &&
            socketException.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return true;
        }

        if (current is IOException ioException &&
            ioException.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static UrlResolutionResult ResolveDevelopmentUrlsWithAutoPortFallback(string? configuredUrls)
{
    var original = configuredUrls;
    var fallbackSource = string.IsNullOrWhiteSpace(configuredUrls)
        ? "http://localhost:5000"
        : configuredUrls;

    var segments = fallbackSource
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .ToList();

    if (segments.Count == 0)
        segments = ["http://localhost:5000"];

    var activePorts = GetActiveListeningPorts();
    var usedByThisResolution = new HashSet<int>();
    var resolvedSegments = new List<string>(segments.Count);
    var portFallbacks = new List<string>();

    foreach (var raw in segments)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            resolvedSegments.Add(raw);
            continue;
        }

        if (!IsLoopbackHost(uri.Host) || uri.Port <= 0)
        {
            resolvedSegments.Add(raw);
            continue;
        }

        var requestedPort = uri.Port;
        var occupied = activePorts.Contains(requestedPort) || usedByThisResolution.Contains(requestedPort);
        if (!occupied)
        {
            usedByThisResolution.Add(requestedPort);
            resolvedSegments.Add(raw);
            continue;
        }

        var nextFree = FindNextAvailablePort(requestedPort + 1, activePorts, usedByThisResolution);
        if (nextFree is null)
        {
            resolvedSegments.Add(raw);
            continue;
        }

        var rebased = new UriBuilder(uri)
        {
            Port = nextFree.Value
        };

        resolvedSegments.Add(rebased.Uri.ToString().TrimEnd('/'));
        usedByThisResolution.Add(nextFree.Value);
        portFallbacks.Add($"{requestedPort}->{nextFree.Value}");
    }

    return new UrlResolutionResult(
        original,
        string.Join(';', resolvedSegments),
        portFallbacks);
}

static HashSet<int> GetActiveListeningPorts()
{
    try
    {
        return System.Net.NetworkInformation.IPGlobalProperties
            .GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Select(ep => ep.Port)
            .ToHashSet();
    }
    catch
    {
        return [];
    }
}

static int? FindNextAvailablePort(int startPort, HashSet<int> activePorts, HashSet<int> reservedPorts)
{
    for (var port = Math.Max(1024, startPort); port <= 65535; port++)
    {
        if (!activePorts.Contains(port) && !reservedPorts.Contains(port))
            return port;
    }

    return null;
}

static bool IsLoopbackHost(string host)
{
    if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        return true;

    if (string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        return true;

    if (string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase))
        return true;

    return false;
}

static int? TryExtractPortFromAddressInUse(Exception ex)
{
    var urlPortPattern = new Regex(@"https?://[^:\s]+:(?<port>\d{2,5})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    for (Exception? current = ex; current != null; current = current.InnerException)
    {
        var urlMatch = urlPortPattern.Match(current.Message);
        if (urlMatch.Success &&
            int.TryParse(urlMatch.Groups["port"].Value, out var parsedFromUrl) &&
            parsedFromUrl is > 0 and <= 65535)
        {
            return parsedFromUrl;
        }
    }

    for (Exception? current = ex; current != null; current = current.InnerException)
    {
        var rawMatches = Regex.Matches(current.Message, @"(?<!\d)(?<port>\d{2,5})(?!\d)");
        foreach (Match rawMatch in rawMatches)
        {
            if (int.TryParse(rawMatch.Groups["port"].Value, out var parsed) && parsed is > 0 and <= 65535)
                return parsed;
        }
    }

    return null;
}

static void LogAddressInUseDiagnostics(int? conflictPort)
{
    try
    {
        var apiProcesses = Process.GetProcessesByName("MathLearning.Api")
            .OrderBy(p => p.Id)
            .ToList();

        if (apiProcesses.Count > 0)
        {
            var processRows = apiProcesses.Select(p => new
            {
                p.Id,
                StartTime = SafeGetStartTime(p),
                Path = SafeGetProcessPath(p.Id)
            });

            Log.Warning("Detected running MathLearning.Api processes: {@Processes}", processRows);
        }
        else
        {
            Log.Warning("No running MathLearning.Api process detected by name.");
        }
    }
    catch (Exception diagEx)
    {
        Log.Warning(diagEx, "Could not enumerate MathLearning.Api processes for diagnostics.");
    }

    if (conflictPort is null)
    {
        Log.Warning("Could not extract conflicting port from exception details.");
        return;
    }

    if (!OperatingSystem.IsWindows())
    {
        Log.Warning("Detailed owning-process diagnostics are currently enabled only on Windows. Conflicting Port={Port}", conflictPort);
        return;
    }

    try
    {
        var listeners = GetWindowsPortListeners(conflictPort.Value);
        if (listeners.Count == 0)
        {
            Log.Warning("No LISTENING entries found by netstat for Port={Port}.", conflictPort);
            return;
        }

        Log.Warning("Port {Port} is currently occupied by: {@Listeners}", conflictPort, listeners);
    }
    catch (Exception diagEx)
    {
        Log.Warning(diagEx, "Could not gather netstat diagnostics for Port={Port}.", conflictPort);
    }
}

static List<object> GetWindowsPortListeners(int port)
{
    var psi = new ProcessStartInfo
    {
        FileName = "netstat",
        Arguments = "-ano -p tcp",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start netstat for diagnostics.");
    var stdout = process.StandardOutput.ReadToEnd();
    process.WaitForExit(3000);

    var listeners = new List<object>();
    var lines = stdout.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    foreach (var line in lines)
    {
        var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
            continue;

        if (!parts[0].Equals("TCP", StringComparison.OrdinalIgnoreCase))
            continue;

        var localAddress = parts[1];
        var state = parts[3];
        var pidRaw = parts[4];

        if (!state.Equals("LISTENING", StringComparison.OrdinalIgnoreCase))
            continue;

        var localPort = ExtractPortFromEndpoint(localAddress);
        if (localPort != port)
            continue;

        if (!int.TryParse(pidRaw, out var pid))
            continue;

        listeners.Add(new
        {
            Port = port,
            Pid = pid,
            ProcessName = SafeGetProcessName(pid),
            Path = SafeGetProcessPath(pid)
        });
    }

    return listeners;
}

static int? ExtractPortFromEndpoint(string endpoint)
{
    var index = endpoint.LastIndexOf(':');
    if (index < 0 || index + 1 >= endpoint.Length)
        return null;

    var segment = endpoint[(index + 1)..].Trim();
    if (int.TryParse(segment, out var port) && port is > 0 and <= 65535)
        return port;

    return null;
}

static string SafeGetProcessName(int pid)
{
    try
    {
        return Process.GetProcessById(pid).ProcessName;
    }
    catch
    {
        return "<unknown>";
    }
}

static string SafeGetProcessPath(int pid)
{
    try
    {
        return Process.GetProcessById(pid).MainModule?.FileName ?? "<unavailable>";
    }
    catch
    {
        return "<unavailable>";
    }
}

static string SafeGetStartTime(Process process)
{
    try
    {
        return process.StartTime.ToString("O");
    }
    catch
    {
        return "<unavailable>";
    }
}

sealed record UrlResolutionResult(
    string? OriginalUrls,
    string Urls,
    IReadOnlyList<string> PortFallbacks);

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}


