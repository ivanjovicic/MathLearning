# BACKEND-CRIT-003 Evidence

Commit SHA: `fa83250fe4e926396f3d93f437446066cda5678a`

Validation command:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "PublicIdentitySurfaceTests|LeaderboardEndpointsIntegrationTests|MobileCompatibilityEndpointsIntegrationTests.UserProfileById_CompatibilityAlias_MatchesCanonicalRoute"
```

Result:

```text
Passed!  - Failed: 0, Passed: 10, Skipped: 0, Total: 10
```

Notes:

- Public search now returns only allowlisted identity fields.
- Public profile no longer mirrors private progress fields.
- Leaderboard responses no longer serialize legacy cosmetic metadata fields.
