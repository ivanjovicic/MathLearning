using MathLearning.Api.Endpoints;
using MathLearning.Api.Services;
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
    if (string.IsNullOrWhiteSpace(defaultConnectionString))
    {
        Log.Error("? ConnectionStrings:Default is not configured. Set Fly secret `ConnectionStrings__Default` (recommended) before deploying.");
        throw new InvalidOperationException("Missing ConnectionStrings:Default. Configure ConnectionStrings__Default.");
    }
    else
    {
        Log.Information("??? DB target ({Environment}): {DbTarget}", builder.Environment.EnvironmentName, DescribeDbConnection(defaultConnectionString));
    }

    // OpenTelemetry (minimal tracing)
    var otelServiceName = builder.Configuration["OpenTelemetry:ServiceName"] ?? "mathlearning-api";
    var otelServiceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
    var enableEfCoreTracing = builder.Configuration.GetValue<bool?>("OpenTelemetry:EnableEntityFrameworkInstrumentation")
        ?? !builder.Environment.IsDevelopment();

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
                .AddHttpClientInstrumentation();

            if (enableEfCoreTracing)
            {
                tracerProviderBuilder.AddEntityFrameworkCoreInstrumentation();
            }

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

    // Hangfire (PostgreSQL)
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(defaultConnectionString), new PostgreSqlStorageOptions
        {
            SchemaName = "hangfire",
            QueuePollInterval = TimeSpan.FromSeconds(15),
            InvisibilityTimeout = TimeSpan.FromMinutes(5)
        }));
    builder.Services.AddHangfireServer();

    // Add Identity (for User management)
    builder.Services.AddIdentityCore<IdentityUser>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 6;
    })
        .AddRoles<IdentityRole>()
        .AddEntityFrameworkStores<ApiDbContext>()
        .AddDefaultTokenProviders();

    // ?? Add Background Services
    builder.Services.AddHostedService<IndexMaintenanceBackgroundService>();
    builder.Services.AddHostedService<XpResetBackgroundService>();

    // ?? Domain Events & Outbox Pattern (In-proc via OutboxProcessor)
    // Keep OutboxProcessor hosted service; use in-proc event bus so background worker invokes local handlers.
    builder.Services.AddScoped<IEventBus, InProcEventBus>();

    // Event handlers (unchanged)
    builder.Services.AddScoped<IEventHandler<QuizCompleted>, QuizCompletedCoinsHandler>();
    builder.Services.AddScoped<IEventHandler<StreakProtectedByFreeze>, FreezeUsedHandler>();
    builder.Services.AddScoped<IEventHandler<CoinsGranted>, CoinsGrantedHandler>();

    // ? SRS service
    builder.Services.AddScoped<ISrsService, SrsService>();
    builder.Services.AddScoped<IAdaptiveLearningService, AdaptiveLearningService>();
    builder.Services.AddScoped<IWeaknessAnalysisService, WeaknessAnalysisService>();
    builder.Services.AddScoped<IQuizAttemptIngestService, QuizAttemptIngestService>();
    builder.Services.AddScoped<IBktService, BktService>();
    builder.Services.AddScoped<IQuestionSelector, EfQuestionSelector>();
    builder.Services.AddScoped<IPracticeAnalyticsUpdater, PracticeAnalyticsUpdater>();
    builder.Services.AddScoped<IPracticeBackgroundJobs, PracticeBackgroundJobs>();
    builder.Services.AddScoped<IPracticeHangfireJobs, PracticeHangfireJobs>();
    builder.Services.AddScoped<ISchoolLeaderboardHangfireJobs, SchoolLeaderboardHangfireJobs>();
    builder.Services.AddScoped<IPracticeSessionService, PracticeSessionService>();
    builder.Services.AddScoped<IMathReasoningGraphEngine, MathReasoningGraphEngine>();
    builder.Services.AddScoped<IStepExplanationGenerator, StepExplanationGenerator>();
    builder.Services.AddScoped<ICommonMistakeDetector, CommonMistakeDetector>();
    builder.Services.AddScoped<IFormulaReferenceService, FormulaReferenceService>();
    builder.Services.AddScoped<IAiTutorEnhancer, AiTutorEnhancer>();
    builder.Services.AddScoped<IExplanationCacheService, ExplanationCacheService>();
    builder.Services.AddScoped<IStepExplanationService, StepExplanationService>();
    builder.Services.AddScoped<LegacyStepExplanationAdapter>();
    builder.Services.AddScoped<AdaptiveApiFacade>();
    builder.Services.AddSingleton<IAdaptiveAnalyticsService, AdaptiveAnalyticsService>();
    builder.Services.AddSingleton<IWeaknessAnalysisScheduler, WeaknessAnalysisScheduler>();
    builder.Services.AddHostedService(sp => (WeaknessAnalysisScheduler)sp.GetRequiredService<IWeaknessAnalysisScheduler>());
    builder.Services.AddHostedService<WeaknessAnalysisDailyHostedService>();

    // In-memory cache + lock (replaces Redis for local / single-node)
    // IMPORTANT: when SizeLimit is set, every cache entry MUST set a Size or IMemoryCache will throw.
    builder.Services.AddMemoryCache(options =>
    {
        // Count-based limit (we set Size=1 per entry in InMemoryCacheService).
        options.SizeLimit = 100;
    });
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddControllers();
    builder.Services.AddValidatorsFromAssemblyContaining<GenerateExplanationRequestValidator>();
    builder.Services.AddSingleton<InMemoryCacheService>();
    builder.Services.AddSingleton<InMemoryLockService>();

    var redisConnectionString = builder.Configuration.GetConnectionString("Redis")
        ?? builder.Configuration["Redis:ConnectionString"];
    if (!string.IsNullOrWhiteSpace(redisConnectionString))
    {
        builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
        builder.Services.AddSingleton<IRedisLeaderboardService, MathLearning.Services.RedisLeaderboardService>();
        Log.Information("?? Redis connection configured for cache-backed features.");
    }
    else
    {
        Log.Warning("Redis connection string is not configured. Redis-backed leaderboard endpoints will not be available.");
    }

    // ? Bug reporting services
    builder.Services.AddScoped<IBugReportService, BugReportService>();
    builder.Services.AddScoped<IScreenshotStorageService, LocalScreenshotStorageService>();

    // ?? Leaderboard service
    // Register the DB-backed LeaderboardService in non-test environments only.
    if (!builder.Environment.IsEnvironment("Test"))
    {
        builder.Services.AddScoped<LeaderboardService>();
        builder.Services.AddScoped<MathLearning.Application.Services.ILeaderboardService>(sp => sp.GetRequiredService<LeaderboardService>());
        builder.Services.AddScoped<ISchoolLeaderboardService>(sp => sp.GetRequiredService<LeaderboardService>());
    }

    // ?? XP tracking service
    builder.Services.AddScoped<SchoolLeaderboardAggregationService>();
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

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(DesignTokenSecurity.AdminPolicy, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(DesignTokenSecurity.AdminRole);
        });
    });

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
        Log.Information("?? Listening on: {Urls}", string.Join(", ", app.Urls));
    });

    // ?? Global exception handler (production-safe problem+json)
    app.UseMiddleware<MathLearning.Api.Middleware.GlobalExceptionMiddleware>();

    // ?? Correlation ID (must be BEFORE request logging so it appears on the request completion log)
    app.UseMiddleware<MathLearning.Api.Middleware.CorrelationIdMiddleware>();

    // ?? Add Serilog request logging
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

    // ??? Auto-migrate and seed database on startup (skip during EF design-time tools)
    if (!EF.IsDesignTime)
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApiDbContext>();
            try
            {
                Log.Information("??? Applying database migrations...");
                await db.Database.MigrateAsync();
                Log.Information("? Database migrations applied successfully");
                databaseStartupSucceeded = true;

                // ?? Manual fix for RefreshToken column length (if migration didn't apply)
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
                    Log.Information("? RefreshToken column length verified");
                }
                catch (Exception fixEx)
                {
                    Log.Warning(fixEx, "?? Could not verify/fix RefreshToken column length");
                }

                // 🎨 Apply cosmetic system tables (idempotent)
                try
                {
                    await db.Database.ExecuteSqlRawAsync(@"
                        CREATE TABLE IF NOT EXISTS cosmetic_seasons (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""Name"" VARCHAR(200) NOT NULL,
                            ""Description"" VARCHAR(1000),
                            ""ThemeAssetPath"" VARCHAR(500),
                            ""StartDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                            ""EndDate"" TIMESTAMP WITH TIME ZONE NOT NULL,
                            ""IsActive"" BOOLEAN NOT NULL DEFAULT FALSE,
                            ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        CREATE TABLE IF NOT EXISTS cosmetic_items (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""Name"" VARCHAR(200) NOT NULL,
                            ""Category"" VARCHAR(50) NOT NULL,
                            ""Rarity"" VARCHAR(20) NOT NULL DEFAULT 'common',
                            ""AssetPath"" VARCHAR(500) NOT NULL,
                            ""PreviewAssetPath"" VARCHAR(500),
                            ""UnlockType"" VARCHAR(50) NOT NULL DEFAULT 'default',
                            ""UnlockCondition"" VARCHAR(500),
                            ""CoinPrice"" INTEGER,
                            ""SeasonId"" INTEGER REFERENCES cosmetic_seasons(""Id"") ON DELETE SET NULL,
                            ""IsDefault"" BOOLEAN NOT NULL DEFAULT FALSE,
                            ""ReleaseDate"" TIMESTAMP WITH TIME ZONE,
                            ""RetirementDate"" TIMESTAMP WITH TIME ZONE,
                            ""CreatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        CREATE TABLE IF NOT EXISTS user_cosmetic_inventory (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""UserId"" VARCHAR(450) NOT NULL,
                            ""CosmeticItemId"" INTEGER NOT NULL REFERENCES cosmetic_items(""Id"") ON DELETE CASCADE,
                            ""Source"" VARCHAR(50) NOT NULL,
                            ""SeasonId"" INTEGER,
                            ""UnlockedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        CREATE TABLE IF NOT EXISTS user_avatar_configs (
                            ""UserId"" VARCHAR(450) PRIMARY KEY REFERENCES ""UserProfiles""(""UserId"") ON DELETE CASCADE,
                            ""SkinId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                            ""HairId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                            ""ClothingId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                            ""AccessoryId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                            ""EmojiId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                            ""FrameId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                            ""BackgroundId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                            ""EffectId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                            ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        -- Indexes
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_seasons_active"" ON cosmetic_seasons (""IsActive"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_seasons_dates"" ON cosmetic_seasons (""StartDate"", ""EndDate"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_category"" ON cosmetic_items (""Category"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_rarity"" ON cosmetic_items (""Rarity"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_category_rarity"" ON cosmetic_items (""Category"", ""Rarity"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_season"" ON cosmetic_items (""SeasonId"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_default"" ON cosmetic_items (""IsDefault"");
                        CREATE UNIQUE INDEX IF NOT EXISTS ""UX_user_cosmetic_inventory_user_item"" ON user_cosmetic_inventory (""UserId"", ""CosmeticItemId"");
                        CREATE INDEX IF NOT EXISTS ""IX_user_cosmetic_inventory_user"" ON user_cosmetic_inventory (""UserId"");
                        CREATE INDEX IF NOT EXISTS ""IX_user_cosmetic_inventory_user_source"" ON user_cosmetic_inventory (""UserId"", ""Source"");
                    ");

                    // Seed default cosmetic items (only if table is empty)
                    var cosmeticCount = await db.CosmeticItems.CountAsync();
                    if (cosmeticCount == 0)
                    {
                        await db.Database.ExecuteSqlRawAsync(@"
                            INSERT INTO cosmetic_items (""Name"", ""Category"", ""Rarity"", ""AssetPath"", ""PreviewAssetPath"", ""UnlockType"", ""IsDefault"", ""ReleaseDate"")
                            VALUES
                                ('Classic Student', 'skin', 'common', 'cosmetics/skin/classic_student.png', 'cosmetics/skin/preview/classic_student.png', 'default', TRUE, NOW()),
                                ('Math Explorer', 'skin', 'common', 'cosmetics/skin/math_explorer.png', 'cosmetics/skin/preview/math_explorer.png', 'default', TRUE, NOW()),
                                ('Short Classic', 'hair', 'common', 'cosmetics/hair/short_classic.png', 'cosmetics/hair/preview/short_classic.png', 'default', TRUE, NOW()),
                                ('Long Straight', 'hair', 'common', 'cosmetics/hair/long_straight.png', 'cosmetics/hair/preview/long_straight.png', 'default', TRUE, NOW()),
                                ('Curly', 'hair', 'common', 'cosmetics/hair/curly.png', 'cosmetics/hair/preview/curly.png', 'default', TRUE, NOW()),
                                ('School Uniform', 'clothing', 'common', 'cosmetics/clothing/school_uniform.png', 'cosmetics/clothing/preview/school_uniform.png', 'default', TRUE, NOW()),
                                ('Casual T-Shirt', 'clothing', 'common', 'cosmetics/clothing/casual_tshirt.png', 'cosmetics/clothing/preview/casual_tshirt.png', 'default', TRUE, NOW()),
                                ('None', 'accessory', 'common', 'cosmetics/accessory/none.png', 'cosmetics/accessory/preview/none.png', 'default', TRUE, NOW()),
                                ('Basic Frame', 'frame', 'common', 'cosmetics/frame/basic.png', 'cosmetics/frame/preview/basic.png', 'default', TRUE, NOW()),
                                ('Classroom', 'background', 'common', 'cosmetics/background/classroom.png', 'cosmetics/background/preview/classroom.png', 'default', TRUE, NOW()),
                                ('Math Board', 'background', 'common', 'cosmetics/background/math_board.png', 'cosmetics/background/preview/math_board.png', 'default', TRUE, NOW());

                            INSERT INTO cosmetic_items (""Name"", ""Category"", ""Rarity"", ""AssetPath"", ""PreviewAssetPath"", ""UnlockType"", ""CoinPrice"", ""IsDefault"", ""ReleaseDate"")
                            VALUES
                                ('Ninja Mathematician', 'skin', 'rare', 'cosmetics/skin/ninja_math.png', 'cosmetics/skin/preview/ninja_math.png', 'shop', 200, FALSE, NOW()),
                                ('Robot Student', 'skin', 'rare', 'cosmetics/skin/robot_student.png', 'cosmetics/skin/preview/robot_student.png', 'shop', 200, FALSE, NOW()),
                                ('Spiky Blue', 'hair', 'rare', 'cosmetics/hair/spiky_blue.png', 'cosmetics/hair/preview/spiky_blue.png', 'shop', 100, FALSE, NOW()),
                                ('Rainbow Ponytail', 'hair', 'rare', 'cosmetics/hair/rainbow_ponytail.png', 'cosmetics/hair/preview/rainbow_ponytail.png', 'shop', 100, FALSE, NOW()),
                                ('Lab Coat', 'clothing', 'rare', 'cosmetics/clothing/lab_coat.png', 'cosmetics/clothing/preview/lab_coat.png', 'shop', 150, FALSE, NOW()),
                                ('Hoodie', 'clothing', 'rare', 'cosmetics/clothing/hoodie.png', 'cosmetics/clothing/preview/hoodie.png', 'shop', 150, FALSE, NOW()),
                                ('Calculator Watch', 'accessory', 'rare', 'cosmetics/accessory/calculator_watch.png', 'cosmetics/accessory/preview/calculator_watch.png', 'shop', 100, FALSE, NOW()),
                                ('Pi Necklace', 'accessory', 'rare', 'cosmetics/accessory/pi_necklace.png', 'cosmetics/accessory/preview/pi_necklace.png', 'shop', 100, FALSE, NOW()),
                                ('Golden Frame', 'frame', 'rare', 'cosmetics/frame/golden.png', 'cosmetics/frame/preview/golden.png', 'shop', 150, FALSE, NOW()),
                                ('Neon Frame', 'frame', 'rare', 'cosmetics/frame/neon.png', 'cosmetics/frame/preview/neon.png', 'shop', 150, FALSE, NOW()),
                                ('Einstein Hair', 'hair', 'epic', 'cosmetics/hair/einstein.png', 'cosmetics/hair/preview/einstein.png', 'shop', 300, FALSE, NOW()),
                                ('Professor Outfit', 'clothing', 'epic', 'cosmetics/clothing/professor.png', 'cosmetics/clothing/preview/professor.png', 'shop', 400, FALSE, NOW()),
                                ('Starfield Background', 'background', 'epic', 'cosmetics/background/starfield.png', 'cosmetics/background/preview/starfield.png', 'shop', 350, FALSE, NOW()),
                                ('Animated Fire Frame', 'frame', 'epic', 'cosmetics/frame/animated_fire.png', 'cosmetics/frame/preview/animated_fire.png', 'shop', 500, FALSE, NOW()),
                                ('Golden Genius Skin', 'skin', 'legendary', 'cosmetics/skin/golden_genius.png', 'cosmetics/skin/preview/golden_genius.png', 'shop', 1000, FALSE, NOW()),
                                ('Galaxy Background', 'background', 'legendary', 'cosmetics/background/galaxy.png', 'cosmetics/background/preview/galaxy.png', 'shop', 800, FALSE, NOW());

                            INSERT INTO cosmetic_items (""Name"", ""Category"", ""Rarity"", ""AssetPath"", ""PreviewAssetPath"", ""UnlockType"", ""UnlockCondition"", ""IsDefault"", ""ReleaseDate"")
                            VALUES
                                ('7-Day Streak Badge', 'accessory', 'rare', 'cosmetics/accessory/streak_7day.png', 'cosmetics/accessory/preview/streak_7day.png', 'streak', 'streak:7', FALSE, NOW()),
                                ('30-Day Streak Crown', 'accessory', 'epic', 'cosmetics/accessory/streak_30day_crown.png', 'cosmetics/accessory/preview/streak_30day_crown.png', 'streak', 'streak:30', FALSE, NOW()),
                                ('100-Day Streak Aura', 'effect', 'legendary', 'cosmetics/effect/streak_100day_aura.png', 'cosmetics/effect/preview/streak_100day_aura.png', 'streak', 'streak:100', FALSE, NOW()),
                                ('Apprentice Hat', 'accessory', 'common', 'cosmetics/accessory/apprentice_hat.png', 'cosmetics/accessory/preview/apprentice_hat.png', 'level', 'level:5', FALSE, NOW()),
                                ('Scholar Robe', 'clothing', 'rare', 'cosmetics/clothing/scholar_robe.png', 'cosmetics/clothing/preview/scholar_robe.png', 'level', 'level:10', FALSE, NOW()),
                                ('Master Frame', 'frame', 'epic', 'cosmetics/frame/master.png', 'cosmetics/frame/preview/master.png', 'level', 'level:25', FALSE, NOW()),
                                ('Grandmaster Skin', 'skin', 'legendary', 'cosmetics/skin/grandmaster.png', 'cosmetics/skin/preview/grandmaster.png', 'level', 'level:50', FALSE, NOW()),
                                ('Top 10 Trophy Frame', 'frame', 'epic', 'cosmetics/frame/top10_trophy.png', 'cosmetics/frame/preview/top10_trophy.png', 'leaderboard', 'top:10', FALSE, NOW()),
                                ('#1 Champion Crown', 'accessory', 'legendary', 'cosmetics/accessory/champion_crown.png', 'cosmetics/accessory/preview/champion_crown.png', 'leaderboard', 'top:1', FALSE, NOW()),
                                ('1000 XP Star', 'effect', 'rare', 'cosmetics/effect/xp_1000_star.png', 'cosmetics/effect/preview/xp_1000_star.png', 'xp_milestone', 'xp:1000', FALSE, NOW()),
                                ('10000 XP Nova', 'effect', 'epic', 'cosmetics/effect/xp_10000_nova.png', 'cosmetics/effect/preview/xp_10000_nova.png', 'xp_milestone', 'xp:10000', FALSE, NOW());
                        ");
                        Log.Information("🎨 Cosmetic items seeded: {Count} default + shop + achievement items", await db.CosmeticItems.CountAsync());
                    }
                    else
                    {
                        Log.Information("🎨 Cosmetic system tables verified ({Count} items exist)", cosmeticCount);
                    }
                    await db.Database.ExecuteSqlRawAsync(@"
                        ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""Key"" VARCHAR(64);
                        ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""Theme"" VARCHAR(128);
                        ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""Status"" VARCHAR(32) NOT NULL DEFAULT 'draft';
                        ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""RewardLockAt"" TIMESTAMP WITH TIME ZONE;
                        ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""ArchiveAt"" TIMESTAMP WITH TIME ZONE;
                        ALTER TABLE cosmetic_seasons ADD COLUMN IF NOT EXISTS ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();

                        UPDATE cosmetic_seasons
                        SET
                            ""Key"" = COALESCE(NULLIF(""Key"", ''), CONCAT('season-', ""Id"")),
                            ""UpdatedAt"" = COALESCE(""UpdatedAt"", NOW())
                        WHERE ""Key"" IS NULL OR ""Key"" = '' OR ""UpdatedAt"" IS NULL;

                        CREATE UNIQUE INDEX IF NOT EXISTS ""UX_cosmetic_seasons_key"" ON cosmetic_seasons (""Key"");

                        ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""Key"" VARCHAR(64);
                        ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""UnlockConditionJson"" jsonb;
                        ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""CompatibilityRulesJson"" jsonb;
                        ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE;
                        ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""IsHidden"" BOOLEAN NOT NULL DEFAULT FALSE;
                        ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""SortOrder"" INTEGER NOT NULL DEFAULT 0;
                        ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""AssetVersion"" VARCHAR(32) NOT NULL DEFAULT '1';
                        ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""MetadataJson"" jsonb;
                        ALTER TABLE cosmetic_items ADD COLUMN IF NOT EXISTS ""UpdatedAt"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW();

                        UPDATE cosmetic_items
                        SET
                            ""Key"" = COALESCE(NULLIF(""Key"", ''), LOWER(REGEXP_REPLACE(CONCAT(""Category"", '-', ""Id"", '-', ""Name""), '[^a-zA-Z0-9]+', '-', 'g'))),
                            ""AssetVersion"" = COALESCE(NULLIF(""AssetVersion"", ''), '1'),
                            ""IsActive"" = COALESCE(""IsActive"", TRUE),
                            ""IsHidden"" = COALESCE(""IsHidden"", FALSE),
                            ""SortOrder"" = COALESCE(""SortOrder"", 0),
                            ""UpdatedAt"" = COALESCE(""UpdatedAt"", ""CreatedAt"", NOW())
                        WHERE
                            ""Key"" IS NULL OR ""Key"" = '' OR
                            ""AssetVersion"" IS NULL OR ""AssetVersion"" = '' OR
                            ""UpdatedAt"" IS NULL;

                        CREATE UNIQUE INDEX IF NOT EXISTS ""UX_cosmetic_items_key"" ON cosmetic_items (""Key"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_items_active_release"" ON cosmetic_items (""IsActive"", ""ReleaseDate"");

                        ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""SourceRef"" VARCHAR(128);
                        ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""GrantReason"" VARCHAR(256);
                        ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""AssetVersion"" VARCHAR(32) NOT NULL DEFAULT '1';
                        ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""IsRevoked"" BOOLEAN NOT NULL DEFAULT FALSE;
                        ALTER TABLE user_cosmetic_inventory ADD COLUMN IF NOT EXISTS ""RevokedAt"" TIMESTAMP WITH TIME ZONE;

                        UPDATE user_cosmetic_inventory
                        SET
                            ""SourceRef"" = COALESCE(""SourceRef"", CONCAT(""Source"", ':', ""CosmeticItemId"")),
                            ""GrantReason"" = COALESCE(""GrantReason"", 'Legacy grant'),
                            ""AssetVersion"" = COALESCE(NULLIF(""AssetVersion"", ''), '1'),
                            ""IsRevoked"" = COALESCE(""IsRevoked"", FALSE)
                        WHERE
                            ""SourceRef"" IS NULL OR
                            ""GrantReason"" IS NULL OR
                            ""AssetVersion"" IS NULL OR ""AssetVersion"" = '';

                        CREATE INDEX IF NOT EXISTS ""IX_user_cosmetic_inventory_source_ref"" ON user_cosmetic_inventory (""Source"", ""SourceRef"");

                        ALTER TABLE user_avatar_configs ADD COLUMN IF NOT EXISTS ""LeaderboardDecorationId"" INTEGER REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL;
                        ALTER TABLE user_avatar_configs ADD COLUMN IF NOT EXISTS ""Version"" BIGINT NOT NULL DEFAULT 0;

                        CREATE TABLE IF NOT EXISTS cosmetic_reward_rules (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""Key"" VARCHAR(128) NOT NULL,
                            ""SourceType"" VARCHAR(64) NOT NULL,
                            ""ConditionJson"" jsonb,
                            ""RewardType"" VARCHAR(64) NOT NULL,
                            ""RewardPayloadJson"" jsonb NOT NULL,
                            ""Priority"" INTEGER NOT NULL DEFAULT 0,
                            ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                            ""CreatedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                            ""UpdatedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        CREATE UNIQUE INDEX IF NOT EXISTS ""UX_cosmetic_reward_rules_key"" ON cosmetic_reward_rules (""Key"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_reward_rules_source_active_priority"" ON cosmetic_reward_rules (""SourceType"", ""IsActive"", ""Priority"");

                        CREATE TABLE IF NOT EXISTS cosmetic_reward_claims (
                            ""Id"" UUID PRIMARY KEY,
                            ""UserId"" VARCHAR(450) NOT NULL,
                            ""RewardKey"" VARCHAR(128) NOT NULL,
                            ""SourceType"" VARCHAR(64) NOT NULL,
                            ""SourceRef"" VARCHAR(128) NOT NULL,
                            ""CosmeticItemId"" INTEGER NOT NULL REFERENCES cosmetic_items(""Id"") ON DELETE CASCADE,
                            ""ClaimedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        CREATE UNIQUE INDEX IF NOT EXISTS ""UX_cosmetic_reward_claims_user_reward_source"" ON cosmetic_reward_claims (""UserId"", ""RewardKey"", ""SourceRef"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_reward_claims_user_claimed_at"" ON cosmetic_reward_claims (""UserId"", ""ClaimedAtUtc"");

                        CREATE TABLE IF NOT EXISTS season_reward_tracks (
                            ""Id"" SERIAL PRIMARY KEY,
                            ""SeasonId"" INTEGER NOT NULL REFERENCES cosmetic_seasons(""Id"") ON DELETE CASCADE,
                            ""TrackType"" VARCHAR(32) NOT NULL,
                            ""Tier"" INTEGER NOT NULL,
                            ""XpRequired"" INTEGER NOT NULL,
                            ""RewardType"" VARCHAR(64) NOT NULL,
                            ""RewardPayloadJson"" jsonb NOT NULL,
                            ""IsActive"" BOOLEAN NOT NULL DEFAULT TRUE,
                            ""CreatedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        CREATE UNIQUE INDEX IF NOT EXISTS ""UX_season_reward_tracks_season_track_tier"" ON season_reward_tracks (""SeasonId"", ""TrackType"", ""Tier"");

                        CREATE TABLE IF NOT EXISTS user_appearance_projection (
                            ""UserId"" VARCHAR(450) PRIMARY KEY,
                            ""AvatarVersion"" BIGINT NOT NULL DEFAULT 0,
                            ""SkinId"" INTEGER NULL,
                            ""HairId"" INTEGER NULL,
                            ""ClothingId"" INTEGER NULL,
                            ""AccessoryId"" INTEGER NULL,
                            ""EmojiId"" INTEGER NULL,
                            ""FrameId"" INTEGER NULL,
                            ""BackgroundId"" INTEGER NULL,
                            ""EffectId"" INTEGER NULL,
                            ""LeaderboardDecorationId"" INTEGER NULL,
                            ""SkinAssetPath"" VARCHAR(500) NULL,
                            ""HairAssetPath"" VARCHAR(500) NULL,
                            ""ClothingAssetPath"" VARCHAR(500) NULL,
                            ""AccessoryAssetPath"" VARCHAR(500) NULL,
                            ""EmojiAssetPath"" VARCHAR(500) NULL,
                            ""FrameAssetPath"" VARCHAR(500) NULL,
                            ""BackgroundAssetPath"" VARCHAR(500) NULL,
                            ""EffectAssetPath"" VARCHAR(500) NULL,
                            ""LeaderboardDecorationAssetPath"" VARCHAR(500) NULL,
                            ""UpdatedAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        CREATE TABLE IF NOT EXISTS cosmetic_telemetry_events (
                            ""Id"" UUID PRIMARY KEY,
                            ""EventType"" VARCHAR(64) NOT NULL,
                            ""UserId"" VARCHAR(450) NOT NULL,
                            ""CosmeticItemId"" INTEGER NULL REFERENCES cosmetic_items(""Id"") ON DELETE SET NULL,
                            ""SeasonId"" INTEGER NULL REFERENCES cosmetic_seasons(""Id"") ON DELETE SET NULL,
                            ""MetadataJson"" jsonb,
                            ""OccurredAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_telemetry_events_type_occurred"" ON cosmetic_telemetry_events (""EventType"", ""OccurredAtUtc"");
                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_telemetry_events_user_occurred"" ON cosmetic_telemetry_events (""UserId"", ""OccurredAtUtc"");

                        CREATE TABLE IF NOT EXISTS cosmetic_audit_log (
                            ""Id"" UUID PRIMARY KEY,
                            ""Action"" VARCHAR(64) NOT NULL,
                            ""ActorUserId"" VARCHAR(450) NULL,
                            ""EntityType"" VARCHAR(64) NOT NULL,
                            ""EntityId"" VARCHAR(128) NOT NULL,
                            ""BeforeJson"" jsonb,
                            ""AfterJson"" jsonb,
                            ""OccurredAtUtc"" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                        );

                        CREATE INDEX IF NOT EXISTS ""IX_cosmetic_audit_log_entity_occurred"" ON cosmetic_audit_log (""EntityType"", ""EntityId"", ""OccurredAtUtc"");
                    ");
                }
                catch (Exception cosmeticEx)
                {
                    Log.Warning(cosmeticEx, "⚠️ Could not apply cosmetic system migration");
                }

                userProfileIdentitySchemaReady = await HasUserProfileIdentitySchemaAsync(db);

                var designTokenQueryService = scope.ServiceProvider.GetRequiredService<IDesignTokenQueryService>();
                await designTokenQueryService.EnsureInitializedAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "?? Database migration/seed failed. Continuing without it (DB may not be available yet).");
            }

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
                Log.Warning("Skipping admin/test user seeding because database startup did not complete successfully.");
            }
            else if (!userProfileIdentitySchemaReady)
            {
                Log.Warning("Skipping admin/test user seeding because UserProfiles.UserId is not compatible with Identity text keys.");
            }
            else
            {
                await SeedAdminUser(app);
            }
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

    // Map Progress endpoints
    app.MapProgressEndpoints();

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
            new { Name = "XP Daily Reset", IsSuccess = true, LastMessage = "Zadnji reset uspeÃƒâ€¦Ã‚Â¡an", Timestamp = now.AddMinutes(-30) },
            new { Name = "Leaderboard Sync", IsSuccess = true, LastMessage = "Leaderboard aÃƒâ€¦Ã‚Â¾uriran", Timestamp = now.AddMinutes(-10) },
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

    if (app.Environment.IsDevelopment())
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

    if (!app.Environment.IsEnvironment("Test") && databaseStartupSucceeded)
    {
        RecurringJob.AddOrUpdate<IPracticeHangfireJobs>(
            "practice-daily-aggregation",
            job => job.DailyAggregationJob(),
            "0 2 * * *");
        RecurringJob.AddOrUpdate<ISchoolLeaderboardHangfireJobs>(
            "school-leaderboard-refresh",
            job => job.RefreshAllCurrentPeriodsJob(),
            "*/10 * * * *");
        RecurringJob.AddOrUpdate<ISchoolLeaderboardHangfireJobs>(
            "school-leaderboard-weekly-snapshot",
            job => job.CaptureSnapshotJob("week"),
            "0 * * * *");
        RecurringJob.AddOrUpdate<ISchoolLeaderboardHangfireJobs>(
            "school-leaderboard-monthly-snapshot",
            job => job.CaptureSnapshotJob("month"),
            "15 */6 * * *");
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
    
    // ?? Seed Test Users for Mobile App
    // 
    // -------------------------------------------------------------------------------
    // ?? TEST USERS (for mobile app login)
    // -------------------------------------------------------------------------------
    // 
    // Username: test     | Password: Test123!
    // Username: demo     | Password: Demo123!
    // Username: ivan     | Password: Ivan123!
    // 
    // -------------------------------------------------------------------------------
    
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
                Log.Information($"? Test user '{testUser.Username}' created successfully!");
                
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
                    Log.Information($"? User profile for '{testUser.Username}' created!");
                }
                else
                {
                    Log.Information($"? User profile for '{testUser.Username}' already exists.");
                }
            }
            else
            {
                Log.Warning($"? Failed to create test user '{testUser.Username}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
        }
        else
        {
            Log.Information($"? Test user '{testUser.Username}' already exists.");
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
                Log.Information($"? User profile for existing user '{testUser.Username}' created!");
            }
        }
    }
     
    // ?? Backfill UserProfiles for Identity users (stable 1:1 mapping)
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
