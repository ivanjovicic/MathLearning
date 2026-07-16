# Refresh Token System

This document describes the current backend auth-session model used by the MathLearning API.

## Goals

- Reject stale access tokens after `POST /auth/revoke-all`.
- Reject refresh tokens whose user/session state is no longer valid, even if the refresh row still exists.
- Keep the model bounded and easy to reason about across multiple app nodes.

## Token model

- Access JWTs carry:
  - `userId`
  - `security_stamp`
  - role claims captured at issuance time
- Refresh tokens are stored in `RefreshTokens` with:
  - `UserId`
  - `Token`
  - `SecurityStamp`
  - expiry/revocation metadata

## Validation rules

- Access tokens are validated on every authenticated request by comparing the JWT `security_stamp` claim with the current Identity user security stamp.
- Locked-out or deleted users are rejected during bearer validation.
- Refresh tokens are accepted only when:
  - the token row is active
  - the user still exists
  - the user is not locked out
  - the stored refresh-token `SecurityStamp` matches the current Identity user security stamp

## Revocation behavior

- `POST /auth/revoke-all` revokes all active refresh tokens for the current user.
- The same request also rotates the user's Identity security stamp.
- Rotating the security stamp invalidates:
  - all currently issued access JWTs
  - any unrevoked refresh token rows that were issued under the previous stamp

## Storage and rollout

- The refresh-token table now stores `SecurityStamp`.
- Migration `20260716151126_AddRefreshTokenSecurityStamp` backfills existing refresh rows from `AspNetUsers.SecurityStamp` when possible.
- The current implementation does not rely on a cache for auth validity, so the database remains the source of truth for session validity.

## Notes

- Refresh token row revocation and stamp rotation happen in the same revoke-all flow.
- If a future change introduces cached auth-state lookups, the cache invalidation window must be documented explicitly.
