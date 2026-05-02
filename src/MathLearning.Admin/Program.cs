using MathLearning.Admin.Components;
using MathLearning.Admin.Data;
using MathLearning.Admin.Services;
using FluentValidation;
using MathLearning.Application.Content;
using MathLearning.Application.DTOs.Questions;
using MathLearning.Application.Services;
using MathLearning.Application.Validators;
using MathLearning.Infrastructure.Services;
using MathLearning.Infrastructure.Services.QuestionAuthoring;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var portEnv = builder.Configuration["PORT"];
if (!string.IsNullOrWhiteSpace(portEnv) && int.TryParse(portEnv, out var port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var adminConnectionString = builder.Configuration.GetConnectionString("AdminIdentity")
    ?? throw new InvalidOperationException(
        "Missing ConnectionStrings:AdminIdentity. Configure ConnectionStrings__AdminIdentity before starting MathLearning.Admin.");

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"];
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("MathLearningAdmin")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    var dataProtectionKeysDirectory = new DirectoryInfo(dataProtectionKeysPath);
    dataProtectionKeysDirectory.Create();
    dataProtectionBuilder.PersistKeysToFileSystem(dataProtectionKeysDirectory);
}
else
{
    dataProtectionBuilder.PersistKeysToDbContext<AdminDbContext>();
}

void ConfigureAdminDbContext(DbContextOptionsBuilder options)
{
    options.UseNpgsql(
        adminConnectionString,
        npgsql => npgsql.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null));
}

builder.Services.AddDbContext<AdminDbContext>(ConfigureAdminDbContext);
builder.Services.AddDbContextFactory<AdminDbContext>(ConfigureAdminDbContext, ServiceLifetime.Scoped);
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

var isProduction = builder.Environment.IsProduction();

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
    options.ReturnUrlParameter = "returnUrl";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = isProduction ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = isProduction ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<ForwardAuthCookiesHandler>();
builder.Services.AddHttpClient<AdminApiClient>((serviceProvider, httpClient) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var apiBaseUrl = configuration["ApiBaseUrl"];
    if (!string.IsNullOrWhiteSpace(apiBaseUrl)
        && Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseUri))
    {
        httpClient.BaseAddress = baseUri;
    }
})
    .AddHttpMessageHandler<ForwardAuthCookiesHandler>();
builder.Services.AddScoped<IMathContentSanitizer, MathContentSanitizer>();
builder.Services.AddScoped<IValidator<QuestionAuthoringRequest>, QuestionAuthoringRequestValidator>();
builder.Services.AddScoped<IQuestionAuthoringService, QuestionAuthoringService>();

// Stateless validation stage services (no ApiDbContext required)
builder.Services.AddScoped<IMathContentLinter, MathContentLinter>();
builder.Services.AddScoped<ILatexValidationService, LatexValidationService>();
builder.Services.AddScoped<IMathNormalizationService, MathNormalizationService>();
builder.Services.AddScoped<IMathEquivalenceService, MathEquivalenceService>();
builder.Services.AddScoped<IStepExplanationValidationService, StepExplanationValidationService>();
builder.Services.AddScoped<IDifficultyEstimationService, DifficultyEstimationService>();
builder.Services.AddScoped<IQuestionPreviewService, QuestionPreviewService>();
builder.Services.AddScoped<IQuestionPublishGuardService, QuestionPublishGuardService>();
builder.Services.AddScoped<AdminQuestionValidationOrchestrator>();

builder.Services.AddMudServices();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(dataProtectionKeysPath))
{
    app.Logger.LogInformation("Data Protection keys are persisted to file system path {Path}", dataProtectionKeysPath);
}
else
{
    app.Logger.LogInformation("Data Protection keys are persisted to AdminDbContext table DataProtectionKeys");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(errorApp =>
    {
        errorApp.Run(async context =>
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync("""
<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>MathLearning Admin Error</title>
</head>
<body>
    <h1>Unexpected error</h1>
    <p>The admin application hit an unexpected error while processing this request.</p>
</body>
</html>
""");
        });
    });
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();

app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseStatusCodePagesWithReExecute("/not-found");
//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

var initializeDatabaseOnStartup = app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Database:InitializeOnStartup", true);

if (initializeDatabaseOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    try
    {
        app.Logger.LogInformation("Applying admin database migrations");
        await db.Database.MigrateAsync();
        app.Logger.LogInformation("Admin database migrations applied successfully");

        app.Logger.LogInformation("Seeding admin database");
        await SeedAdminAsync(app);
        app.Logger.LogInformation("Admin database seeding complete");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Admin startup failed while applying migrations or seeding data");
        throw;
    }
}
else
{
    app.Logger.LogInformation("Skipping admin database initialization on startup because Database:InitializeOnStartup is disabled.");
}

app.MapGet("/health", () => Results.Ok("Healthy"));
app.MapGet("/healthz", () => Results.Ok("Healthy"));
app.MapGet("/favicon.ico", () => Results.NoContent());
app.MapGet("/login", RenderLoginPage).AllowAnonymous();
app.MapGet("/login-page", RenderLoginPage).AllowAnonymous();

app.MapPost("/api/account/login", async (
    HttpContext httpContext,
    SignInManager<IdentityUser> signInManager,
    ILoggerFactory loggerFactory) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var logger = loggerFactory.CreateLogger("MathLearning.Admin.Login");
    var form = await httpContext.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    returnUrl = ReturnUrlSanitizer.NormalizeLocalReturnUrl(returnUrl);

    var result = await signInManager.PasswordSignInAsync(username, password, isPersistent: true, lockoutOnFailure: false);
    stopwatch.Stop();
    if (result.Succeeded)
    {
        logger.LogInformation("Admin login succeeded for {Username} in {ElapsedMs} ms", username, stopwatch.ElapsedMilliseconds);
        return Results.Redirect(returnUrl);
    }

    logger.LogWarning("Admin login failed for {Username} in {ElapsedMs} ms", username, stopwatch.ElapsedMilliseconds);
    return Results.Redirect($"/login?error=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
}).DisableAntiforgery();

app.MapPost("/api/account/logout", async (SignInManager<IdentityUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/login");
}).DisableAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapRazorPages();

app.Run();

async Task SeedAdminAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

    // Seed domain data first (skip if tables don't exist, e.g., when migrations were skipped)
    try
    {
        await DbSeeder.SeedAsync(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Domain data seeding failed (tables may not exist); skipping and continuing with admin user seeding.");
    }

    // Create admin role if not exists
    if (!await roleManager.RoleExistsAsync("Admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("Admin"));
    }

    var seedAdminEnabled =
        app.Environment.IsDevelopment()
        || app.Configuration.GetValue<bool>("SeedAdmin:Enabled");

    if (!seedAdminEnabled)
    {
        app.Logger.LogInformation("SeedAdmin is disabled. Skipping admin user bootstrap.");
        return;
    }

    var adminUsername = app.Configuration["SeedAdmin:Username"] ?? "admin";
    var adminEmail = app.Configuration["SeedAdmin:Email"] ?? "admin@mathlearning.com";
    var adminPassword =
        app.Configuration["SeedAdmin:Password"]
        ?? (app.Environment.IsDevelopment() ? "UcimMatu!123" : null);
    var resetPasswordOnStart = app.Configuration.GetValue<bool>("SeedAdmin:ResetPasswordOnStart");

    if (string.IsNullOrWhiteSpace(adminPassword))
    {
        app.Logger.LogWarning(
            "SeedAdmin is enabled but SeedAdmin:Password is not configured. Set SeedAdmin__Password to create or reset the admin account.");
        return;
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
            app.Logger.LogInformation("Admin user '{Username}' created successfully", adminUsername);
        }
        else
        {
            throw new InvalidOperationException(
                "Failed to create admin user: " + string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }
    else
    {
        if (!string.Equals(adminUser.Email, adminEmail, StringComparison.OrdinalIgnoreCase))
        {
            adminUser.Email = adminEmail;
            adminUser.NormalizedEmail = adminEmail.ToUpperInvariant();

            var updateResult = await userManager.UpdateAsync(adminUser);
            if (!updateResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Failed to update admin user: " + string.Join(", ", updateResult.Errors.Select(e => e.Description)));
            }
        }

        if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }

        if (resetPasswordOnStart)
        {
            // Re-fetch to get latest ConcurrencyStamp after any prior updates
            adminUser = (await userManager.FindByNameAsync(adminUsername))!;
            await EnsurePasswordAsync(userManager, adminUser, adminPassword);
            app.Logger.LogInformation("Admin password reset on startup for '{Username}'", adminUsername);
        }

        app.Logger.LogInformation("Admin user '{Username}' already exists", adminUsername);
    }
}

static async Task EnsurePasswordAsync(UserManager<IdentityUser> userManager, IdentityUser user, string password)
{
    var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
    var resetResult = await userManager.ResetPasswordAsync(user, resetToken, password);
    if (!resetResult.Succeeded)
    {
        throw new InvalidOperationException(
            "Failed to reset admin password: " + string.Join(", ", resetResult.Errors.Select(e => e.Description)));
    }
}

static IResult RenderLoginPage(HttpContext httpContext)
{
    var returnUrl = ReturnUrlSanitizer.NormalizeLocalReturnUrl(httpContext.Request.Query["returnUrl"].ToString());
    if (httpContext.User.Identity?.IsAuthenticated == true)
    {
        if (returnUrl.Equals("/login", StringComparison.OrdinalIgnoreCase)
            || returnUrl.StartsWith("/login?", StringComparison.OrdinalIgnoreCase)
            || returnUrl.Equals("/login-page", StringComparison.OrdinalIgnoreCase)
            || returnUrl.StartsWith("/login-page?", StringComparison.OrdinalIgnoreCase))
        {
            returnUrl = "/";
        }

        return Results.Redirect(returnUrl);
    }

    httpContext.Response.Headers.CacheControl = "no-store";
    httpContext.Response.Headers.Pragma = "no-cache";

    var hasError = !string.IsNullOrWhiteSpace(httpContext.Request.Query["error"].ToString());
    var encodedReturnUrl = HtmlEncode(returnUrl);
    var errorMarkup = hasError
        ? """<div class="alert" role="alert">Neispravno korisnicko ime ili lozinka.</div>"""
        : string.Empty;

    var html = $$"""
<!doctype html>
<html lang="sr">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Login - MathLearning Admin</title>
    <style>
        :root {
            color-scheme: light;
            --primary: #594ae2;
            --primary-dark: #4035a8;
            --text: rgba(0, 0, 0, 0.87);
            --muted: rgba(0, 0, 0, 0.62);
            --border: rgba(0, 0, 0, 0.20);
            --danger: #c62828;
        }
        * { box-sizing: border-box; }
        body {
            min-height: 100vh;
            margin: 0;
            display: grid;
            place-items: center;
            padding: 24px;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: var(--text);
            font-family: "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
        }
        main {
            width: min(100%, 420px);
            padding: 32px;
            border-radius: 8px;
            background: #fff;
            box-shadow: 0 12px 36px rgba(0, 0, 0, 0.24);
        }
        h1 {
            margin: 0 0 20px;
            font-size: 2rem;
            font-weight: 500;
        }
        form {
            display: grid;
            gap: 16px;
        }
        label {
            display: block;
            margin-bottom: 8px;
            font-weight: 500;
            color: rgba(0, 0, 0, 0.78);
        }
        input {
            width: 100%;
            min-height: 52px;
            padding: 14px;
            border: 1px solid var(--border);
            border-radius: 4px;
            background: #fff;
            color: var(--text);
            font: inherit;
        }
        input:focus {
            outline: none;
            border-color: var(--primary);
            box-shadow: 0 0 0 1px var(--primary);
        }
        .password-field {
            position: relative;
        }
        .password-field input {
            padding-right: 52px;
        }
        .toggle-password {
            position: absolute;
            right: 8px;
            bottom: 8px;
            width: 36px;
            height: 36px;
            border: 0;
            border-radius: 50%;
            background: transparent;
            color: var(--muted);
            cursor: pointer;
            font-size: 0.78rem;
        }
        .toggle-password:hover,
        .toggle-password:focus-visible {
            background: rgba(0, 0, 0, 0.06);
            color: var(--text);
            outline: none;
        }
        .submit {
            width: 100%;
            min-height: 48px;
            border: 0;
            border-radius: 4px;
            background: var(--primary);
            color: #fff;
            font: inherit;
            font-weight: 600;
            cursor: pointer;
        }
        .submit:hover,
        .submit:focus-visible {
            background: var(--primary-dark);
            outline: none;
        }
        .alert {
            margin-bottom: 16px;
            padding: 12px 14px;
            border-radius: 4px;
            background: #ffebee;
            color: var(--danger);
        }
        .caption {
            margin: 18px 0 0;
            text-align: center;
            color: var(--muted);
            font-size: 0.82rem;
        }
    </style>
</head>
<body>
    <main>
        <h1>Prijava</h1>
        {{errorMarkup}}
        <form method="post" action="/api/account/login" autocomplete="on">
            <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}">
            <div>
                <label for="username">Korisnicko ime</label>
                <input id="username" name="username" type="text" autocomplete="username" spellcheck="false" required autofocus>
            </div>
            <div class="password-field">
                <label for="password">Lozinka</label>
                <input id="password" name="password" type="password" autocomplete="current-password" required>
                <button id="toggle-password" class="toggle-password" type="button" aria-label="Prikazi lozinku">Prikazi</button>
            </div>
            <button class="submit" type="submit">Prijavi se</button>
        </form>
        <p class="caption">Koristite administratorske kredencijale iz konfiguracije okruzenja.</p>
    </main>
    <script>
        document.getElementById('toggle-password')?.addEventListener('click', function () {
            const input = document.getElementById('password');
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            this.textContent = show ? 'Sakrij' : 'Prikazi';
            this.setAttribute('aria-label', show ? 'Sakrij lozinku' : 'Prikazi lozinku');
        });
    </script>
</body>
</html>
""";

    return Results.Content(html, "text/html; charset=utf-8");
}

static string HtmlEncode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
