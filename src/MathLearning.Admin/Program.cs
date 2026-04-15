using MathLearning.Admin.Components;
using MathLearning.Admin.Data;
using MathLearning.Admin.Services;
using MathLearning.Application.Content;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using MathLearning.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

var portEnv = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var dpBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("MathLearningAdmin")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

if (!builder.Environment.IsDevelopment())
{
    dpBuilder.PersistKeysToFileSystem(new DirectoryInfo("/app/keys"));
}

builder.Services.AddDbContext<AdminDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("AdminIdentity")));
builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AdminDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
    options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
})
.AddCookie(IdentityConstants.ApplicationScheme, options =>
{
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ForwardAuthCookiesHandler>();

builder.Services.AddHttpClient<AdminApiClient>(client =>
{
    var configuredBaseUrl = builder.Configuration["ApiBaseUrl"];
    if (Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri))
    {
        client.BaseAddress = baseUri;
    }
})
    .AddHttpMessageHandler<ForwardAuthCookiesHandler>();

builder.Services.AddMudServices();
builder.Services.AddScoped<IMathContentSanitizer, MathContentSanitizer>();

builder.Services.AddServerSideBlazor();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHealthChecks();

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

// Configure the HTTP request pipeline.
app.UseForwardedHeaders(forwardedHeadersOptions);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Auto-migrate and seed database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var configuredApiBaseUrl = app.Configuration["ApiBaseUrl"];
    try
    {
        if (!Uri.TryCreate(configuredApiBaseUrl, UriKind.Absolute, out _))
        {
            logger.LogWarning("ApiBaseUrl is not configured as an absolute URL. Admin API calls will fall back to the current host.");
        }

        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied successfully");

        logger.LogInformation("Seeding database...");
        await SeedAdminAsync(app, logger);
        logger.LogInformation("Database seeding complete");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database migration/seed failed");
    }
}

app.MapRazorComponents<App>()   
    .AddInteractiveServerRenderMode();

app.MapHealthChecks("/healthz");

app.Run();

async Task SeedAdminAsync(WebApplication app, ILogger logger)
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

    var contentSeedEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("SeedContent:Enabled");
    if (contentSeedEnabled)
    {
        await DbSeeder.SeedAsync(db);
    }
    else
    {
        logger.LogInformation("Content seeding skipped. Set `SeedContent__Enabled=true` to run DbSeeder on startup.");
    }

    // Create admin role if not exists
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    var seedAdminEnabled = app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("SeedAdmin:Enabled");
    if (!seedAdminEnabled)
    {
        logger.LogInformation("Admin seeding skipped. Set `SeedAdmin__Enabled=true` to enable it outside development.");
        return;
    }

    var adminUsername = app.Configuration["SeedAdmin:Username"]
        ?? Environment.GetEnvironmentVariable("ADMIN_SEED_USERNAME")
        ?? "admin";
    var adminPassword = app.Configuration["SeedAdmin:Password"]
        ?? Environment.GetEnvironmentVariable("ADMIN_SEED_PASSWORD")
        ?? (app.Environment.IsDevelopment() ? "UcimMatu!123" : null);
    var adminEmail = app.Configuration["SeedAdmin:Email"]
        ?? Environment.GetEnvironmentVariable("ADMIN_SEED_EMAIL")
        ?? "admin@mathlearning.com";
    var resetAdminPasswordOnStart =
        app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("SeedAdmin:ResetPasswordOnStart");

    if (string.IsNullOrWhiteSpace(adminPassword))
    {
        logger.LogWarning("SeedAdmin enabled but no password was provided. Set `SeedAdmin__Password` to create or reset the admin user.");
        return;
    }

    static async Task EnsurePasswordAsync(UserManager<IdentityUser> userManager, IdentityUser user, string password)
    {
        if (await userManager.HasPasswordAsync(user))
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            await userManager.ResetPasswordAsync(user, resetToken, password);
            return;
        }

        await userManager.AddPasswordAsync(user, password);
    }

    var adminUser = await userManager.FindByNameAsync(adminUsername);
    if (adminUser == null)
    {
        adminUser = new IdentityUser
        {
            UserName = adminUsername,
            Email = adminEmail,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            logger.LogInformation("Admin user created successfully");
        }
        else
        {
            logger.LogError("Failed to create admin user: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
    else
    {
        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        if (resetAdminPasswordOnStart)
        {
            await EnsurePasswordAsync(userManager, adminUser, adminPassword);
            logger.LogInformation("Admin password ensured on startup (SeedAdmin:ResetPasswordOnStart).");
        }

        logger.LogInformation("Admin user already exists");
    }
}

