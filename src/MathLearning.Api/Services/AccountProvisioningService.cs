using MathLearning.Domain.Entities;
using MathLearning.Infrastructure.Persistance;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MathLearning.Api.Services;

/// <summary>
/// Single owner for mandatory Identity + UserProfile provisioning.
/// Tokens are issued by auth endpoints only after this service reports a complete account.
/// </summary>
public interface IAccountProvisioningService
{
    Task<AccountProvisionResult> CreateCompleteAccountAsync(
        string username,
        string email,
        string password,
        string? displayName,
        CancellationToken cancellationToken = default);

    Task<bool> HasCompleteProfileAsync(string userId, CancellationToken cancellationToken = default);

    Task<IncompleteAccountScanResult> ScanIncompleteAccountsAsync(CancellationToken cancellationToken = default);
}

public sealed record AccountProvisionResult(
    bool Succeeded,
    bool Conflict,
    bool ValidationFailed,
    IdentityUser? User,
    UserProfile? Profile);

public sealed record IncompleteAccountScanResult(
    int IdentityOnlyCount,
    int ProfileOnlyCount,
    int RefreshTokenWithoutProfileCount,
    IReadOnlyList<string> IdentityOnlyUserIds);

public sealed class AccountProvisioningService : IAccountProvisioningService
{
    private readonly UserManager<IdentityUser> userManager;
    private readonly ApiDbContext db;

    public AccountProvisioningService(UserManager<IdentityUser> userManager, ApiDbContext db)
    {
        this.userManager = userManager;
        this.db = db;
    }

    public async Task<AccountProvisionResult> CreateCompleteAccountAsync(
        string username,
        string email,
        string password,
        string? displayName,
        CancellationToken cancellationToken = default)
    {
        var canonicalUsername = username.Trim();
        var canonicalEmail = email.Trim();

        if (await userManager.FindByNameAsync(canonicalUsername) != null
            || await userManager.FindByEmailAsync(canonicalEmail) != null)
        {
            return new AccountProvisionResult(
                Succeeded: false,
                Conflict: true,
                ValidationFailed: false,
                User: null,
                Profile: null);
        }

        var user = new IdentityUser
        {
            UserName = canonicalUsername,
            Email = canonicalEmail,
            EmailConfirmed = true,
            LockoutEnabled = true
        };

        var createResult = await userManager.CreateAsync(user, password);
        if (!createResult.Succeeded)
        {
            return new AccountProvisionResult(
                Succeeded: false,
                Conflict: false,
                ValidationFailed: true,
                User: null,
                Profile: null);
        }

        var now = DateTime.UtcNow;
        var profile = new UserProfile
        {
            UserId = user.Id,
            Username = canonicalUsername,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? canonicalUsername : displayName.Trim(),
            Coins = 100,
            Level = 1,
            Xp = 0,
            Streak = 0,
            CreatedAt = now,
            UpdatedAt = now
        };

        try
        {
            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Identity CreateAsync may already be committed when no ambient relational
            // transaction enlists UserManager. Compensate before rethrowing so callers
            // never leave a usable Identity-only incomplete account.
            await CompensateIncompleteProvisionAsync(user, cancellationToken);
            throw;
        }

        return new AccountProvisionResult(
            Succeeded: true,
            Conflict: false,
            ValidationFailed: false,
            User: user,
            Profile: profile);
    }

    private async Task CompensateIncompleteProvisionAsync(
        IdentityUser user,
        CancellationToken cancellationToken)
    {
        try
        {
            var trackedProfile = await db.UserProfiles
                .SingleOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
            if (trackedProfile != null)
            {
                db.UserProfiles.Remove(trackedProfile);
                await db.SaveChangesAsync(cancellationToken);
            }

            var persistedUser = await userManager.FindByIdAsync(user.Id);
            if (persistedUser != null)
                await userManager.DeleteAsync(persistedUser);
        }
        catch
        {
            // Best-effort cleanup; original failure is rethrown by the caller.
        }
    }

    public Task<bool> HasCompleteProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        return db.UserProfiles.AsNoTracking().AnyAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<IncompleteAccountScanResult> ScanIncompleteAccountsAsync(
        CancellationToken cancellationToken = default)
    {
        var identityIds = await db.Users.AsNoTracking()
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        var profileIds = await db.UserProfiles.AsNoTracking()
            .Select(p => p.UserId)
            .ToListAsync(cancellationToken);

        var identitySet = identityIds.ToHashSet(StringComparer.Ordinal);
        var profileSet = profileIds.ToHashSet(StringComparer.Ordinal);

        var identityOnly = identityIds
            .Where(id => !profileSet.Contains(id))
            .ToList();
        var profileOnlyCount = profileIds.Count(id => !identitySet.Contains(id));

        var tokenUserIds = await db.RefreshTokens.AsNoTracking()
            .Select(t => t.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var refreshWithoutProfile = tokenUserIds.Count(id => !profileSet.Contains(id));

        return new IncompleteAccountScanResult(
            IdentityOnlyCount: identityOnly.Count,
            ProfileOnlyCount: profileOnlyCount,
            RefreshTokenWithoutProfileCount: refreshWithoutProfile,
            IdentityOnlyUserIds: identityOnly);
    }
}
