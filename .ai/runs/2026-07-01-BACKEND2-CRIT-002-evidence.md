# BACKEND2-CRIT-002 Evidence

Commit SHA: `79ea851175434f20726799dc761ab32065fdb6df`

Validation command:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "AuthRefreshConcurrencyTests|AuthRefreshEndpointRegressionTests"
```

Result:

```text
Passed!  - Failed: 0, Passed: 4, Skipped: 0, Total: 4
```

Notes:

- `RefreshToken.RevokedAt` is now an EF concurrency token, so concurrent refresh rotations collide instead of minting multiple active descendants.
- The broader `RefreshToken|Auth|Concurrency` filter also matches an unrelated SQLite concurrency test in this repository, so the narrower auth-focused filter was used to validate this prompt cleanly.
- Endpoint regression coverage proves sequential reuse returns unauthorized and logout/revoke-all still revoke refresh tokens.
