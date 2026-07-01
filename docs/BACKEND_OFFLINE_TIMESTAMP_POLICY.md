# Backend offline answer timestamp policy

Aligned: 2026-07-01  
Prompt: `BACKEND-CRIT-007`

## Accepted offline replay window

- **Future skew:** up to 2 minutes ahead of server UTC (`OfflineAnswerTimestampPolicy.MaxFutureSkew`)
- **Maximum age:** 90 days behind server UTC (`OfflineAnswerTimestampPolicy.MaxReplayAge`)

Timestamps outside this window are skipped and reported in `OfflineBatchSubmitResponse.issues`.

## Normalization rules

- Parse legacy `answeredAt` strings with `DateTimeOffset` round-trip semantics.
- Convert all accepted timestamps to **UTC** and truncate to **millisecond** precision for duplicate detection.
- Missing `answeredAt` in legacy batch-submit uses server UTC now and returns issue code `answered_at_defaulted`.
- Malformed `answeredAt` is rejected with `invalid_timestamp` (no silent import).

## Calendar authority

Streak and daily activity use `DateOnly.FromDateTime(normalizedUtc)` from the normalized offline timestamp, not raw local/unspecified values.
