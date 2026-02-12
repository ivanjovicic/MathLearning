using MathLearning.Api.Endpoints;
using MathLearning.Api.Services;
using MathLearning.Application.Services;
using MathLearning.Infrastructure.Persistance;
using MathLearning.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Events;
using System.Data.Common;
using System.Text;

// 📝 Configure Serilog EARLY (before builder)
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/mathlearning-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("🚀 Starting MathLearning API");

    var builder = WebApplication.CreateBuilder(args);

    // 📝 Use Serilog for logging
    builder.Host.UseSerilog();

    var defaultConnectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(defaultConnectionString))
    {
        Log.Warning("⚠️ ConnectionStrings:Default is not configured.");
    }
    else
    {
        Log.Information("🗄️ DB target ({Environment}): {DbTarget}", builder.Environment.EnvironmentName, DescribeDbConnection(defaultConnectionString));
    }

    // Add ApiDbContext (kombinuje Identity i sve entitete)
    builder.Services.AddDbContext<ApiDbContext>(options =>
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

    // ✅ SRS service
    builder.Services.AddScoped<ISrsService, SrsService>();

    // ✅ Bug reporting services
    builder.Services.AddScoped<IBugReportService, BugReportService>();
    builder.Services.AddScoped<IScreenshotStorageService, LocalScreenshotStorageService>();

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

    // 📝 Add Serilog request logging
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent);
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
        await SeedAdminUser(app);
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Admin seeding failed, continuing without seeding.");
    }

    // Configure the HTTP request pipeline.
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

    app.UseAuthentication();
    app.UseAuthorization();

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
    var adminUsername = "admin";
    var existingAdmin = await userManager.FindByNameAsync(adminUsername);
    
    if (existingAdmin == null)
    {
        var admin = new IdentityUser
        {
            UserName = adminUsername,
            Email = "admin@mathlearning.com",
            EmailConfirmed = true
        };
        
        var result = await userManager.CreateAsync(admin, "UcimMatu!123");
        
        if (result.Succeeded)
        {
            Log.Information("✓ Admin user created successfully!");
            
            // Create UserProfile for admin
            int adminUserId = int.TryParse(admin.Id, out var id) ? id : Math.Abs(admin.Id.GetHashCode());
            
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
                int userId = int.TryParse(user.Id, out var id) ? id : Math.Abs(user.Id.GetHashCode());
                
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
                int userId = int.TryParse(existingUser.Id, out var id) ? id : Math.Abs(existingUser.Id.GetHashCode());
                
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
    
    // 🔗 Create IdentityUser for existing UserProfiles (from DbSeeder) that don't have Identity accounts
    var existingProfiles = await db.UserProfiles.ToListAsync();
    
    foreach (var profile in existingProfiles)
    {
        var identityUser = await userManager.FindByNameAsync(profile.Username);
        
        if (identityUser == null)
        {
            // Create IdentityUser for this profile
            var newIdentityUser = new IdentityUser
            {
                UserName = profile.Username,
                Email = $"{profile.Username}@mathlearning.com",
                EmailConfirmed = true
            };
            
            var result = await userManager.CreateAsync(newIdentityUser, "Default123!");
            
            if (result.Succeeded)
            {
                Log.Information($"✓ Identity account created for existing profile '{profile.Username}' (Password: Default123!)");
            }
            else
            {
                Log.Warning($"✗ Failed to create Identity account for '{profile.Username}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }
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

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
