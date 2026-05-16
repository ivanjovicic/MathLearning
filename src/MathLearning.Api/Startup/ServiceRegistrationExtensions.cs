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
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;

namespace MathLearning.Api.Startup;

public static class ServiceRegistrationExtensions
{
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

    public static void AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddHostedService<IndexMaintenanceBackgroundService>();
        builder.Services.AddHostedService<XpResetBackgroundService>();

        builder.Services.AddScoped<IEventBus, InProcEventBus>();
        builder.Services.AddScoped<IEventHandler<QuizCompleted>, QuizCompletedCoinsHandler>();
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
        builder.Services.AddSingleton<IWeaknessAnalysisScheduler, WeaknessAnalysisScheduler>();
        builder.Services.AddHostedService(sp => (WeaknessAnalysisScheduler)sp.GetRequiredService<IWeaknessAnalysisScheduler>());
        builder.Services.AddHostedService<WeaknessAnalysisDailyHostedService>();

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

    public static void AddSecurityServices(this WebApplicationBuilder builder)
    {
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
    }

    public static void AddCorsAndSwagger(this WebApplicationBuilder builder)
    {
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

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
    }
}
