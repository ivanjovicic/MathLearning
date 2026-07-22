using System.Text;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using MathLearning.Api.Services;
using MathLearning.Application.Services;
using MathLearning.Application.Validators;
using MathLearning.Core.Services;
using MathLearning.Domain.Events;
using MathLearning.Infrastructure;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.EventBus;
using MathLearning.Infrastructure.Services.EventBus.Handlers;
using MathLearning.Infrastructure.Services.Leaderboard;
using MathLearning.Infrastructure.Services.Performance;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

namespace MathLearning.Api.Startup;

public static class ServiceRegistrationExtensions
{
    private const string FallbackJwtSecret = "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";

    public static void AddObservabilityServices(this WebApplicationBuilder builder)
    {
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
    }

    public static void AddDatabaseServices(this WebApplicationBuilder builder, string defaultConnectionString, bool isDevelopment)
    {
        var healthChecks = builder.Services.AddHealthChecks();
        if (!builder.Environment.IsEnvironment("Test") && !string.IsNullOrWhiteSpace(defaultConnectionString))
        {
            healthChecks.AddNpgSql(defaultConnectionString, name: "postgres");
        }

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<PerformanceDbCommandInterceptor>();
        builder.Services.AddSingleton<DatabaseSchemaState>();
        builder.Services.AddSingleton<DatabaseSchemaVersionGuard>();

        builder.Services.AddDbContext<ApiDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                    defaultConnectionString,
                    npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            options.AddInterceptors(sp.GetRequiredService<PerformanceDbCommandInterceptor>());

            if (isDevelopment)
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
                options.LogTo(s => Log.Debug("EF: {Message}", s), Microsoft.Extensions.Logging.LogLevel.Information);
            }
        });

        builder.Services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                    defaultConnectionString,
                    npgsql => npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery));
            options.AddInterceptors(sp.GetRequiredService<PerformanceDbCommandInterceptor>());

            if (isDevelopment)
            {
                options.EnableDetailedErrors();
                options.EnableSensitiveDataLogging();
                options.LogTo(s => Log.Debug("EF: {Message}", s), Microsoft.Extensions.Logging.LogLevel.Information);
            }
        });
    }

    public static async Task<bool> AddBackgroundJobServices(
        this WebApplicationBuilder builder,
        string defaultConnectionString,
        Func<string, CancellationToken, Task<bool>> canOpenPostgresConnectionAsync,
        CancellationToken ct = default)
    {
        var hangfireEnabled = false;

        if (!builder.Environment.IsEnvironment("Test"))
        {
            if (await canOpenPostgresConnectionAsync(defaultConnectionString, ct))
            {
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
                hangfireEnabled = true;
            }
            else
            {
                builder.Services.AddSingleton<IBackgroundJobClient, DisabledBackgroundJobClient>();
                Log.Warning(
                    "Skipping Hangfire startup because PostgreSQL is unavailable at startup. Background job enqueue calls will be logged and ignored until the service restarts with a reachable database.");
            }
        }
        else
        {
            builder.Services.AddSingleton<IBackgroundJobClient, DisabledBackgroundJobClient>();
        }

        return hangfireEnabled;
    }

    public static void AddApplicationLayerServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<IndexMaintenanceBackgroundService>();
        builder.Services.AddHostedService<XpResetBackgroundService>();
        builder.Services.AddHostedService<OutboxProcessor>();

        builder.Services.AddScoped<IEventBus, InProcEventBus>();
        builder.Services.AddScoped<OutboxBatchProcessor>();
        builder.Services.AddScoped<IEventHandler<QuizCompleted>, QuizCompletedCoinsHandler>();
        builder.Services.AddScoped<IEventHandler<QuizAttemptIngestRequested>, QuizAttemptIngestRequestedHandler>();
        builder.Services.AddScoped<IEventHandler<StreakProtectedByFreeze>, FreezeUsedHandler>();
        builder.Services.AddScoped<IEventHandler<CoinsGranted>, CoinsGrantedHandler>();

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
        builder.Services.AddScoped<IAntiCheatHangfireJobs, AntiCheatHangfireJobs>();
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
        builder.Services.AddScoped<MathLearning.Infrastructure.Services.CosmeticLoadoutProjectionService>();
        builder.Services.AddSingleton<IAdaptiveAnalyticsService, AdaptiveAnalyticsService>();
        builder.Services.Configure<WeaknessAnalysisSchedulerOptions>(
            builder.Configuration.GetSection(WeaknessAnalysisSchedulerOptions.SectionName));
        builder.Services.AddSingleton<IWeaknessAnalysisScheduler, WeaknessAnalysisScheduler>();
        builder.Services.AddHostedService(sp => (WeaknessAnalysisScheduler)sp.GetRequiredService<IWeaknessAnalysisScheduler>());
        builder.Services.AddHostedService<WeaknessAnalysisDailyHostedService>();
    }

    public static void AddCacheAndInfrastructureServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
        });

        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddControllers();
        builder.Services.AddValidatorsFromAssemblyContaining<GenerateExplanationRequestValidator>();
        builder.Services.AddSingleton<InMemoryCacheService>();
        builder.Services.AddSingleton<InMemoryLockService>();
        builder.Services.AddScoped<RequestDataCacheService>();

        var redisConnectionString = ResolveRedisConnectionString(builder.Configuration);
        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            try
            {
                var redisOptions = BuildRedisConfigurationOptions(builder.Configuration, redisConnectionString);
                var redisMultiplexer = ConnectionMultiplexer.Connect(redisOptions);

                builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);
                builder.Services.AddSingleton<IRedisLeaderboardService, MathLearning.Services.RedisLeaderboardService>();
                Log.Information(
                    "Redis connection configured for cache-backed features. ConnectTimeoutMs={ConnectTimeoutMs} SyncTimeoutMs={SyncTimeoutMs} ConnectRetry={ConnectRetry} KeepAliveSeconds={KeepAliveSeconds} AbortOnConnectFail={AbortOnConnectFail}",
                    redisOptions.ConnectTimeout,
                    redisOptions.SyncTimeout,
                    redisOptions.ConnectRetry,
                    redisOptions.KeepAlive,
                    redisOptions.AbortOnConnectFail);
            }
            catch (Exception ex)
            {
                builder.Services.AddScoped<IRedisLeaderboardService, DbBackedRedisLeaderboardService>();
                Log.Warning(
                    ex,
                    "Redis startup initialization failed. Falling back to DB-backed leaderboard service.");
            }
        }
        else
        {
            builder.Services.AddScoped<IRedisLeaderboardService, DbBackedRedisLeaderboardService>();
            Log.Warning("Redis connection string is not configured. Falling back to DB-backed leaderboard service.");
        }

        builder.Services.AddScoped<IBugReportService, BugReportService>();
        builder.Services.AddScoped<IScreenshotStorageService, LocalScreenshotStorageService>();

        if (!builder.Environment.IsEnvironment("Test"))
        {
            builder.Services.AddScoped<LeaderboardService>();
            builder.Services.AddScoped<MathLearning.Application.Services.ILeaderboardService>(sp => sp.GetRequiredService<LeaderboardService>());
            builder.Services.AddScoped<ISchoolLeaderboardService>(sp => sp.GetRequiredService<LeaderboardService>());
        }

        builder.Services.AddScoped<SchoolLeaderboardAggregationService>();
        builder.Services.Configure<XpTrackingOptions>(
            builder.Configuration.GetSection(XpTrackingOptions.SectionName));
        builder.Services.AddScoped<XpTrackingService>();
        builder.Services.AddScoped<IXpTrackingService>(sp => sp.GetRequiredService<XpTrackingService>());
        builder.Services.AddScoped<StudentLeaderboardService>();
        builder.Services.AddScoped<IStudentLeaderboardService>(sp => sp.GetRequiredService<StudentLeaderboardService>());
    }

    public static ConfigurationOptions BuildRedisConfigurationOptions(
        IConfiguration configuration,
        string redisConnectionString)
    {
        var options = ConfigurationOptions.Parse(redisConnectionString);
        options.AbortOnConnectFail = configuration.GetValue<bool?>("Redis:AbortOnConnectFail") ?? false;
        options.ConnectTimeout = configuration.GetValue<int?>("Redis:ConnectTimeoutMs") ?? 2000;
        options.SyncTimeout = configuration.GetValue<int?>("Redis:SyncTimeoutMs") ?? 2000;
        options.ConnectRetry = configuration.GetValue<int?>("Redis:ConnectRetry") ?? 3;
        options.KeepAlive = configuration.GetValue<int?>("Redis:KeepAliveSeconds") ?? 60;

        var defaultDatabase = configuration.GetValue<int?>("Redis:DefaultDatabase");
        if (defaultDatabase.HasValue)
        {
            options.DefaultDatabase = defaultDatabase.Value;
        }

        return options;
    }

    private static string? ResolveRedisConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("Redis")
        ?? configuration["Redis:ConnectionString"];

    public static void AddSecurityServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<MathLearning.Api.Middleware.IRateLimitCounterStore, MathLearning.Api.Middleware.InMemoryRateLimitCounterStore>();
        builder.Services.AddScoped<AuthSessionValidationService>();

        builder.Services.AddIdentityCore<IdentityUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireUppercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 10;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApiDbContext>()
            .AddSignInManager<SignInManager<IdentityUser>>()
            .AddDefaultTokenProviders();

        var jwtSettings = builder.Configuration.GetSection("JwtSettings");
        var secretKey = ResolveJwtSecret(builder);
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

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var sessionValidation = context.HttpContext.RequestServices
                            .GetRequiredService<AuthSessionValidationService>();

                        if (context.Principal is null || !await sessionValidation.IsAccessTokenCurrentAsync(context.Principal))
                        {
                            context.Fail("The access token is no longer valid.");
                        }
                    }
                };
            });

        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(DesignTokenSecurity.AdminPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(DesignTokenSecurity.AdminRole);
            });

            options.AddPolicy(DesignTokenSecurity.ContentAuthorPolicy, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(
                    DesignTokenSecurity.AdminRole,
                    DesignTokenSecurity.ContentAuthorRole);
            });
        });
    }

    private static string ResolveJwtSecret(WebApplicationBuilder builder)
    {
        var secretKey = builder.Configuration.GetSection("JwtSettings")["SecretKey"];
        var isDevelopmentOrTest = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test");

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            if (isDevelopmentOrTest)
            {
                secretKey = FallbackJwtSecret;
            }
            else
            {
                Log.Error("JwtSettings:SecretKey must be configured outside Development/Test.");
                throw new InvalidOperationException("JwtSettings:SecretKey must be configured outside Development/Test.");
            }
        }

        if (!isDevelopmentOrTest && string.Equals(secretKey, FallbackJwtSecret, StringComparison.Ordinal))
        {
            Log.Error("JwtSettings:SecretKey must be configured outside Development/Test.");
            throw new InvalidOperationException("JwtSettings:SecretKey must be configured outside Development/Test.");
        }

        if (secretKey.Length < 32)
        {
            Log.Error("JwtSettings:SecretKey must be at least 32 characters.");
            throw new InvalidOperationException("JwtSettings:SecretKey must be at least 32 characters.");
        }

        return secretKey;
    }

    public static void AddApiDocumentationServices(this WebApplicationBuilder builder)
        => AddCorsAndSwagger(builder);

    public static void AddCorsAndSwagger(this WebApplicationBuilder builder)
    {
        var isDevelopmentOrTest = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test");
        var allowedOrigins = Array.Empty<string>();

        if (!isDevelopmentOrTest)
        {
            allowedOrigins = GetConfiguredCorsAllowedOrigins(builder.Configuration);
            if (allowedOrigins.Length == 0)
            {
                Log.Error("Cors:AllowedOrigins must be configured outside Development/Test.");
                throw new InvalidOperationException("Cors:AllowedOrigins must be configured outside Development/Test.");
            }
        }

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (isDevelopmentOrTest)
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .SetPreflightMaxAge(TimeSpan.FromMinutes(30));
                    return;
                }

                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .SetPreflightMaxAge(TimeSpan.FromMinutes(30));
            });
        });

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }

    private static string[] GetConfiguredCorsAllowedOrigins(IConfiguration configuration)
    {
        var section = configuration.GetSection("Cors:AllowedOrigins");
        var origins = section.Get<string[]>() ?? [];

        if (origins.Length == 0)
        {
            var rawOrigins = configuration["Cors:AllowedOrigins"];
            if (!string.IsNullOrWhiteSpace(rawOrigins))
            {
                origins = rawOrigins.Split(
                    [',', ';'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            }
        }

        return origins
            .Where(origin => !string.IsNullOrWhiteSpace(origin))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
