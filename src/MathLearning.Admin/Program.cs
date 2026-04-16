using MathLearning.Admin.Components;
using MathLearning.Admin.Data;
using MathLearning.Admin.Services;
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

var adminConnectionString = builder.Configuration.GetConnectionString("AdminIdentity")
    ?? throw new InvalidOperationException(
        "Missing ConnectionStrings:AdminIdentity. Configure ConnectionStrings__AdminIdentity before starting MathLearning.Admin.");

var dataProtectionKeysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? (builder.Environment.IsDevelopment()
        ? Path.Combine(builder.Environment.ContentRootPath, "keys")
        : "/tmp/mathlearning-admin-keys");

Directory.CreateDirectory(dataProtectionKeysPath);

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath))
    .SetApplicationName("MathLearningAdmin")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

builder.Services.AddDbContext<AdminDbContext>(options =>
    options.UseNpgsql(adminConnectionString));
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

builder.Services.AddMudServices();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
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

// Auto-migrate and seed database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
    try
    {
        await using var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT to_regclass('public.\"AspNetRoles\"') IS NOT NULL";

        var identitySchemaExists = false;
        var result = await command.ExecuteScalarAsync();
        if (result is bool exists)
        {
            identitySchemaExists = exists;
        }

        var forceSeed = app.Configuration.GetValue<bool>("SeedAdmin:Force");

        if (identitySchemaExists && !forceSeed)
        {
            app.Logger.LogInformation("Detected existing Identity schema. Skipping automatic admin startup migrations and seeding.");
        }
        else if (identitySchemaExists && forceSeed)
        {
            app.Logger.LogInformation("SeedAdmin:Force=true; skipping migrations (tables already exist) but running seeding.");
            await SeedAdminAsync(app);
            app.Logger.LogInformation("Admin database seeding complete");
        }
        else
        {
            app.Logger.LogInformation("Applying admin database migrations");
            await db.Database.MigrateAsync();
            app.Logger.LogInformation("Admin database migrations applied successfully");

            app.Logger.LogInformation("Seeding admin database");
            await SeedAdminAsync(app);
            app.Logger.LogInformation("Admin database seeding complete");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Admin startup failed while applying migrations or seeding data");
        throw;
    }
}

app.MapGet("/health", () => Results.Ok("Healthy"));
app.MapGet("/favicon.ico", () => Results.NoContent());

app.MapPost("/api/account/login", async (HttpContext httpContext, SignInManager<IdentityUser> signInManager) =>
{
    var form = await httpContext.Request.ReadFormAsync();
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(returnUrl) || !returnUrl.StartsWith('/') || returnUrl.StartsWith("//"))
        returnUrl = "/";

    var result = await signInManager.PasswordSignInAsync(username, password, isPersistent: true, lockoutOnFailure: false);
    if (result.Succeeded)
    {
        return Results.Redirect(returnUrl);
    }

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

