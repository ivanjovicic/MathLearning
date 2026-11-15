# USER-20261115-A5-LEADERBOARD-WEEK-BOUNDARY Evidence

Evidence format: v2
Prompt ID: USER-20261115-A5-LEADERBOARD-WEEK-BOUNDARY
Queue: user-assigned
Agent/tool: unknown-not-exposed
Model provider: unknown-not-exposed
Model name/id: unknown-not-exposed
Client/IDE: unknown-not-exposed
Run mode: investigation
Token budget: low
Started at UTC: 2026-11-15T22:07:14Z
Completed at UTC: 2026-11-15T22:18:35Z
Elapsed time: 11m 21s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001, BACKEND-MISTAKE-VALIDATION-001, BACKEND-MISTAKE-CI-001, BACKEND-MISTAKE-PROCESS-001
How this run avoids prior mistakes: apply BACKEND-MISTAKE-EVIDENCE-001; apply BACKEND-MISTAKE-VALIDATION-001; apply BACKEND-MISTAKE-CI-001; apply BACKEND-MISTAKE-PROCESS-001
Owner/hypothesis: The two Sunday tests used test-owned week arithmetic over a frozen Sunday fixture; the historical formula advances the start to Monday and excludes all seeded answers. Production reset and leaderboard period owners use Monday-start UTC. Falsifier: old formula does not produce Expected 1 / Actual 0 under the same isolated InMemory fixtures.
Files inspected: 13
Files changed: 4
Searches: 5
Validation runs: 9
Failed retries: 0

## Outcome
- Reproduced red deterministically by substituting the exact old Sunday formula against the fixed 2026-11-01 Sunday fixture: both variants failed Expected 1 / Actual 0. Confirmed `TestDbContextFactory.Create` uses a unique EF InMemory database, ruling out PostgreSQL translation and shared-database isolation. The query is a test-local `UserAnswers` count; production leaderboard ranks stored `UserProfile.WeeklyXp`, while reset policy uses a separate Monday-start UTC calculation.
- Updated both weekly fixture tests to use `SchoolLeaderboardPeriods.StartOfWeekUtc`, include Sunday and Monday 00:00 UTC, and exclude the previous Sunday tick. Added year-crossing weekly-reset counterexamples for previous Sunday, same-week Dec 28, and Monday Jan 4. No runtime code changed.

## Changed paths
- `tests/MathLearning.Tests/Endpoints/LeaderboardDataTests.cs`
- `tests/MathLearning.Tests/Services/LeaderboardDataTests.cs`
- `tests/MathLearning.Tests/Services/XpResetProcessorTests.cs`
- `.ai/runs/2026-11-15-USER-20261115-A5-LEADERBOARD-WEEK-BOUNDARY-evidence.md`

## Validation
Validation run: SDK 8.0.422 / net8.0. Red `dotnet test` filter for `Leaderboard_WeeklyXp_OnlyCountsThisWeek`: exit 1, both tests failed Expected 1 / Actual 0 with fixed Nov 1 date and old formula. Green combined filter for both leaderboard tests and `Create_WeeklyResetUsesUtcMondayBoundaryAcrossYearTransition`: exit 0, 5/5. Repeated leaderboard-only filter twice with `--no-build`: exit 0, 2/2 each run. `dotnet format whitespace`, `style`, and `analyzers` with `--verify-no-changes` on the three changed C# files: exit 0 each. Three `check_documentation_health.py --context <changed test>` checks and `git diff --check`: exit 0.
Validation not run: PostgreSQL suite/provider validation not applicable: the reproduced failure and affected fixture use EF InMemory, and the defect is UTC calendar arithmetic rather than SQL translation or database timestamp behavior. No production data was touched.

## Exceptions and learning
Mistakes observed: none
Waste: Initial formatter invocations were read-only; no unnecessary builds or hosted runs.
Missed: Initial low-budget investigation exceeded its inspection/search/change-path limits; preserved as an incomplete run rather than masking the overrun.
Follow-up: Delivery completed in the bounded validation-only continuation recorded at `.ai/runs/2026-11-15-USER-20261115-A5-LEADERBOARD-WEEK-BOUNDARY-DELIVERY-evidence.md`; no additional source scope.
Residual risk: The two historical `Leaderboard_WeeklyXp_OnlyCountsThisWeek` tests are in-memory fixture arithmetic rather than direct assertions of the production XP ranking query; production uses `WeeklyXp` profile values and was reviewed without runtime changes.
Documentation impact: No durable documentation change; run log records test-only evidence.
Cross-repo impact: none

## Delivery
State: Needs evidence sync
Branch/PR: codex/a5-leaderboard-week-boundary; PR #59 merged
Commit SHA: self
Completion %: 79
