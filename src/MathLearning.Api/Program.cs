using MathLearning.Api.Endpoints;
using MathLearning.Api.Services;
using MathLearning.Application.Services;
using MathLearning.Domain.Events;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.EventBus;
using MathLearning.Infrastructure.Services.EventBus.Handlers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Data.Common;
using System.Text;

// 📝 Configure Serilog EARLY (before builder)
var startupEnvironment =
    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? "Production";

var isDevelopment = string.Equals(startupEnvironment, "Development", StringComparison.OrdinalIgnoreCase);

var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Warning()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
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
    Log.Information("🚀 Starting MathLearning API");

    var builder = WebApplication.CreateBuilder(args);

    // 📝 Use Serilog for logging
    builder.Host.UseSerilog();

    // Fly.io / container safety: some platforms set PORT instead of ASPNETCORE_URLS.
    // Only apply when PORT is explicitly provided so local dev keeps its default ports.
    var portEnv = builder.Configuration["PORT"];
    if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        Log.Information("🌐 Binding to PORT from environment: {Port}", port);
    }

    var defaultConnectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(defaultConnectionString))
    {
        Log.Error("❌ ConnectionStrings:Default is not configured. Set Fly secret `ConnectionStrings__Default` (recommended) before deploying.");
        throw new InvalidOperationException("Missing ConnectionStrings:Default. Configure ConnectionStrings__Default.");
    }
    else
    {
        Log.Information("🗄️ DB target ({Environment}): {DbTarget}", builder.Environment.EnvironmentName, DescribeDbConnection(defaultConnectionString));
    }

    // OpenTelemetry (minimal tracing)
    var otelServiceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "mathlearning-api";
    var otelServiceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    builder.Services.AddOpenTelemetry()
        .ConfigureResource(resource => resource.AddService(
            serviceName: otelServiceName,
            serviceVersion: otelServiceVersion))
        .WithTracing(tracerProviderBuilder =>
        {
            tracerProviderBuilder
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = true;
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation();

            // Optional OTLP exporter (Jaeger/Tempo/etc). If not configured, dev uses console exporter.
            var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                ?? builder.Configuration["OpenTelemetry:Otlp:Endpoint"];

            if (builder.Environment.IsDevelopment())
            {
                tracerProviderBuilder.AddConsoleExporter();
            }

            if (!string.IsNullOrWhiteSpace(otlpEndpoint))
            {
                tracerProviderBuilder.AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                });
            }
        });

    // Health checks (Fly / uptime monitoring)
    var healthChecks = builder.Services.AddHealthChecks();
    if (!string.IsNullOrWhiteSpace(defaultConnectionString))
    {
        healthChecks.AddNpgSql(defaultConnectionString, name: "postgres");
    }

    // Add ApiDbContext (kombinuje Identity i sve entitete)
    builder.Services.AddDbContext<ApiDbContext>(options =>
        options.UseNpgsql(
            defaultConnectionString,
            npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

    // Add AppDbContext (domain/outbox context used by event handlers + OutboxProcessor)
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(
            defaultConnectionString,
            npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

    // Add Identity (for User management)
    builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
        .AddEntityFrameworkStores<ApiDbContext>()
        .AddDefaultTokenProviders();

    // 🔧 Add Background Services
    builder.Services.AddHostedService<IndexMaintenanceBackgroundService>();
    builder.Services.AddHostedService<XpResetBackgroundService>();

    // 🎯 Domain Events & Outbox Pattern (In‑proc via OutboxProcessor)
    // Keep OutboxProcessor hosted service; use in‑proc event bus so background worker invokes local handlers.
    builder.Services.AddScoped<IEventBus, InProcEventBus>();

    // Event handlers (unchanged)
    builder.Services.AddScoped<IEventHandler<QuizCompleted>, QuizCompletedCoinsHandler>();
    builder.Services.AddScoped<IEventHandler<StreakProtectedByFreeze>, FreezeUsedHandler>();
    builder.Services.AddScoped<IEventHandler<CoinsGranted>, CoinsGrantedHandler>();

    // ✅ SRS service
    builder.Services.AddScoped<ISrsService, SrsService>();

    // In-memory cache + lock (replaces Redis for local / single-node)
    // IMPORTANT: when SizeLimit is set, every cache entry MUST set a Size or IMemoryCache will throw.
    builder.Services.AddMemoryCache(options =>
    {
        // Count-based limit (we set Size=1 per entry in InMemoryCacheService).
        options.SizeLimit = 100;
    });
    builder.Services.AddSingleton<InMemoryCacheService>();
    builder.Services.AddSingleton<InMemoryLockService>();

    // ✅ Bug reporting services
    builder.Services.AddScoped<IBugReportService, BugReportService>();
    builder.Services.AddScoped<IScreenshotStorageService, LocalScreenshotStorageService>();

    // 🏆 Leaderboard service
    builder.Services.AddScoped<LeaderboardService>();

    // 📈 XP tracking service
    builder.Services.AddScoped<XpTrackingService>();

    // Configure JWT Authentication
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
    var issuer = jwtSettings["Issuer"] ?? "MathLearningAPI";
    var audience = jwtSettings["Audience"] ?? "MathLearningApp";

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
            };
        });

    builder.Services.AddAuthorization();

    // Configure CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .SetPreflightMaxAge(TimeSpan.FromMinutes(30));
        });
    });

    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        Log.Information("🌐 Listening on: {Urls}", string.Join(", ", app.Urls));
    });

    // 🧯 Global exception handler (production-safe problem+json)
    app.UseMiddleware<MathLearning.Api.Middleware.GlobalExceptionMiddleware>();

    // 🔗 Correlation ID (must be BEFORE request logging so it appears on the request completion log)
    app.UseMiddleware<MathLearning.Api.Middleware.CorrelationIdMiddleware>();

    // 📝 Add Serilog request logging
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

    // 🗄️ Auto-migrate and seed database on startup (skip during EF design-time tools)
    if (!EF.IsDesignTime)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            try
            {
                Log.Information("🗄️ Applying database migrations...");
                await db.Database.MigrateAsync();
                Log.Information("✅ Database migrations applied successfully");

                // 🔧 Manual fix for RefreshToken column length (if migration didn't apply)
                try
                {
                    await db.Database.ExecuteSqlRawAsync(@"
                        DO $$
                        BEGIN
                            IF EXISTS (
                                SELECT 1 FROM information_schema.columns 
                                WHERE table_name = 'RefreshTokens' 
                                AND column_name = 'Token' 
                                AND character_maximum_length = 64
                            ) THEN
                                ALTER TABLE ""RefreshTokens"" ALTER COLUMN ""Token"" TYPE character varying(128);
                                RAISE NOTICE 'RefreshToken column extended to 128 characters';
                            END IF;
                        END $$;
                    ");
                    Log.Information("✅ RefreshToken column length verified");
                }
                catch (Exception fixEx)
                {
                    Log.Warning(fixEx, "⚠️ Could not verify/fix RefreshToken column length");
                }

                Log.Information("🌱 Seeding database...");
                await DbSeeder.SeedAsync(db);
                Log.Information("✅ Database seeding complete");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "⚠️ Database migration/seed failed. Continuing without it (DB may not be available yet).");
            }
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
            await SeedAdminUser(app);
        }
        else
        {
            Log.Warning("SeedAdmin is disabled (set `SeedAdmin__Enabled=true` to enable). Skipping admin/test user seeding.");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Admin seeding failed, continuing without seeding.");
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
        Log.Information("🔓 HTTPS redirection disabled in Development mode");
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

    // Map User endpoints
    app.MapUserEndpoints();

    // Map Quiz endpoints
    app.MapQuizEndpoints();

    // Map Hint endpoints
    app.MapHintEndpoints();

    // Map Coin endpoints
    app.MapCoinEndpoints();

    // Map Powerup endpoints
    app.MapPowerupEndpoints();

    // Map Progress endpoints
    app.MapProgressEndpoints();

    // Map Leaderboard endpoints
    app.MapLeaderboardEndpoints();

    // Map Maintenance endpoints
    app.MapMaintenanceEndpoints();

    // Map Logging endpoints
    app.MapLoggingEndpoints();

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

    Log.Information("✅ MathLearning API started successfully");
    
    app.Run();
}
catch (Microsoft.Extensions.Hosting.HostAbortedException)
{
    // Expected when EF Core tooling uses the startup project to build the host and then aborts it.
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Application terminated unexpectedly");
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
    var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();

    static async Task EnsurePasswordAsync(UserManager<IdentityUser> userManager, IdentityUser user, string password)
    {
        if (await userManager.HasPasswordAsync(user))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, resetToken, password);
            if (!resetResult.Succeeded)
            {
                Log.Warning($"✗ Failed to reset password for '{user.UserName}': {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            var addResult = await userManager.AddPasswordAsync(user, password);
            if (!addResult.Succeeded)
            {
                Log.Warning($"✗ Failed to add password for '{user.UserName}': {string.Join(", ", addResult.Errors.Select(e => e.Description))}");
            }
        }
    }
    
    // 👤 Seed Admin User
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
            Log.Information("✓ Admin user created successfully!");
            
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
                Log.Information("✓ Admin user profile created!");
            }
        }
        else
        {
            Log.Warning($"✗ Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
    else
    {
        Log.Information("✓ Admin user already exists.");
        if (resetAdminPasswordOnStart)
        {
            await EnsurePasswordAsync(userManager, existingAdmin, adminPassword);
            Log.Information("✓ Admin password ensured on startup (SeedAdmin:ResetPasswordOnStart).");
        }
    }
    
    // 📱 Seed Test Users for Mobile App
    // 
    // ═══════════════════════════════════════════════════════════════════════════════
    // 🔑 TEST USERS (for mobile app login)
    // ═══════════════════════════════════════════════════════════════════════════════
    // 
    // Username: test     | Password: Test123!
    // Username: demo     | Password: Demo123!
    // Username: ivan     | Password: Ivan123!
    // 
    // ═══════════════════════════════════════════════════════════════════════════════
    
    var seedTestUsersEnabled =
        app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("SeedTestUsers:Enabled");

    if (!seedTestUsersEnabled)
    {
        Log.Warning("SeedTestUsers is disabled (set `SeedTestUsers__Enabled=true` to enable). Skipping test user seeding.");
        return;
    }

    var testUsers = new[]
    {
        new { Username = "test", Email = "test@mathlearning.com", Password = "Test123!", DisplayName = "Test User", Coins = 100, Level = 1, Xp = 0, Streak = 0 },
        new { Username = "demo", Email = "demo@mathlearning.com", Password = "Demo123!", DisplayName = "Demo User", Coins = 150, Level = 2, Xp = 50, Streak = 3 },
        new { Username = "ivan", Email = "ivan@mathlearning.com", Password = "Ivan123!", DisplayName = "Ivan", Coins = 300, Level = 3, Xp = 200, Streak = 5 }
    };
    
    foreach (var testUser in testUsers)
    {
        var existingUser = await userManager.FindByNameAsync(testUser.Username);
        
        if (existingUser == null)
        {
            var user = new IdentityUser
            {
                UserName = testUser.Username,
                Email = testUser.Email,
                EmailConfirmed = true
            };
            
            var result = await userManager.CreateAsync(user, testUser.Password);
            
            if (result.Succeeded)
            {
                Log.Information($"✓ Test user '{testUser.Username}' created successfully!");
                
                 // Create UserProfile
                 string userId = user.Id;
                 
                 // Check by Username (unique constraint) instead of UserId
                 if (!await db.UserProfiles.AnyAsync(p => p.Username == testUser.Username))
                 {
                     var userProfile = new MathLearning.Domain.Entities.UserProfile
                     {
                         UserId = userId,
                        Username = testUser.Username,
                        DisplayName = testUser.DisplayName,
                        Coins = testUser.Coins,
                        Level = testUser.Level,
                        Xp = testUser.Xp,
                        Streak = testUser.Streak,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    db.UserProfiles.Add(userProfile);
                    await db.SaveChangesAsync();
                    Log.Information($"✓ User profile for '{testUser.Username}' created!");
                }
                else
                {
                    Log.Information($"✓ User profile for '{testUser.Username}' already exists.");
                }
            }
            else
            {
                Log.Warning($"✗ Failed to create test user '{testUser.Username}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            Log.Information($"✓ Test user '{testUser.Username}' already exists.");
            await EnsurePasswordAsync(userManager, existingUser, testUser.Password);
              
            // Ensure UserProfile exists even if IdentityUser was created before
            if (!await db.UserProfiles.AnyAsync(p => p.Username == testUser.Username))
            {
                string userId = existingUser.Id;
                 
                var userProfile = new MathLearning.Domain.Entities.UserProfile
                {
                    UserId = userId,
                    Username = testUser.Username,
                    DisplayName = testUser.DisplayName,
                    Coins = testUser.Coins,
                    Level = testUser.Level,
                    Xp = testUser.Xp,
                    Streak = testUser.Streak,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                db.UserProfiles.Add(userProfile);
                await db.SaveChangesAsync();
                Log.Information($"✓ User profile for existing user '{testUser.Username}' created!");
            }
        }
    }
     
    // 🔗 Backfill UserProfiles for Identity users (stable 1:1 mapping)
    var allIdentityUsers = await userManager.Users.ToListAsync();
    foreach (var u in allIdentityUsers)
    {
        if (await db.UserProfiles.AnyAsync(p => p.UserId == u.Id))
            continue;

        db.UserProfiles.Add(new MathLearning.Domain.Entities.UserProfile
        {
            UserId = u.Id,
            Username = u.UserName ?? u.Id,
            DisplayName = u.UserName ?? u.Id,
            Coins = 100,
            Level = 1,
            Xp = 0,
            Streak = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
    }
    await db.SaveChangesAsync();
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

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
