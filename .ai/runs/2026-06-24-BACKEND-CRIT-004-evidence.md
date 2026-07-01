# BACKEND-CRIT-004 Evidence

Prompt ID: BACKEND-CRIT-004
Queue: `docs/prompt_queues/backend_critical_risk_prevention.md`
Run mode: implementation/test
Relevant prior mistakes read: BACKEND-MISTAKE-AUDIT-001, BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001

## Files changed

- `src/MathLearning.Api/Services/LegacyAvatarUploadValidator.cs` (new)
- `src/MathLearning.Api/Endpoints/UserEndpoints.cs`
- `src/MathLearning.Api/Program.cs`
- `tests/MathLearning.Tests/Endpoints/LegacyAvatarUploadSafetyTests.cs` (new)
- `tests/MathLearning.Tests/Services/LegacyAvatarUploadValidatorTests.cs` (new)
- `docs/API_ENDPOINT_INVENTORY.md`

## What was done

- Added legacy avatar upload validation: 2MB max, extension allowlist (jpg/jpeg/png/webp), magic-byte sniffing, content-type mismatch rejection.
- Server-generated filenames with user prefix; path traversal blocked on read.
- Aligned avatar storage to `ContentRootPath/uploads/avatars`.
- Blocked public static serving for `/uploads/avatars/*` (auth-gated route only).
- Tests for unsupported extension, oversize file, spoofed content-type, static bypass, and cross-user fetch denial.

## Validation run

```bash
dotnet test --filter "Avatar|Upload|StaticFiles|Users"
```

**Passed: 43, Failed: 0**

## Risk prevented

- **avatar-upload-safety**: untrusted uploads cannot bypass type/size checks or fetch other users' avatars via static files.

## Commit SHA

95156ed
