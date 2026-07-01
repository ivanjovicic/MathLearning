# BACKEND2-CRIT-003 Evidence

Commit SHA: `b073350d67677a87bcb6ecf74b6b3f9156c100d4`

Validation command:

```bash
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "AuthMobileRegistrationAtomicityTests"
```

Result:

```text
Passed!  - Failed: 0, Passed: 3, Skipped: 0, Total: 3
```

Notes:

- Mobile registration now uses a relational transaction when available and a cleanup fallback for non-relational test providers.
- Partial registration failures clean up Identity user, profile, and refresh token state.
- Retry after a partial failure succeeds once and keeps welcome coins at the expected default value.
