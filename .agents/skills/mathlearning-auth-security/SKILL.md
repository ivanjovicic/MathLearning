---
name: mathlearning-auth-security
description: Investigate or change MathLearning authentication, JWT/refresh tokens, roles, cross-user access, rate limiting, password reset and private user data.
---
# Auth and privacy boundary
1. Read `AGENTS.md`, auth endpoints/services, token configuration and nearest authenticated test host; find the actual owner of the identity claim.
2. Never trust body `userId` as authority. Compare route user target to authenticated actor; distinguish admin actor and target. Review persistence/cache scope per user.
3. Prove anonymous, valid user, wrong user, wrong role, expired/revoked JWT, rotated refresh token, logout and deleted-user behavior as relevant.
4. For reset/password operations verify rate limiting, token replay, token expiration, no account enumeration and no secrets in logs/errors. Test actual middleware behavior, not an attribute string.
5. Confirm old-user cache/session does not persist across account changes. Client-side success cannot authorize server-side mutation.
6. Add a focused negative integration test, select guarded verification from `.ai/VALIDATION_SELECTOR.md`, update existing durable API docs only for actual contract changes and record exact-head CI.
**Stop:** permissions weakening, production secret/credential change, destructive user data update or missing authorization policy.
