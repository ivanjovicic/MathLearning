# BACKEND-TEST-036 Evidence

Prompt ID: BACKEND-TEST-036
Queue: `docs/prompt_queues/backend_test_coverage.md`
Agent/tool: ChatGPT via GitHub connector
Model provider: OpenAI
Model name/id: GPT-5.5 Thinking
Run mode: direct unit/contract coverage for previously unprompted gaps
Started from queue status: new implementation package requested by user

## Goal

Increase coverage in high-value pure logic and HTTP contract areas not explicitly owned by BACKEND-TEST-022…035, while avoiding new persistence architecture or mobile contract changes.

## Relevant prior mistakes

- BACKEND-MISTAKE-EVIDENCE-001
- BACKEND-MISTAKE-VALIDATION-001
- BACKEND-MISTAKE-IDEM-001
- BACKEND-MISTAKE-VALIDATION-002
- BACKEND-MISTAKE-CONTENT-001
- BACKEND-MISTAKE-CONTENT-002

## Test files added

- `tests/MathLearning.Tests/Helpers/UserIdGuidMapperTests.cs`
- `tests/MathLearning.Tests/Services/IdempotencyObservabilityServiceTests.cs`
- `tests/MathLearning.Tests/Endpoints/IdempotencyObservabilityAuthorizationTests.cs`
- `tests/MathLearning.Tests/Helpers/InlineLatexFormatterTests.cs`
- `tests/MathLearning.Tests/Endpoints/InlineLatexEndpointContractTests.cs`
- `tests/MathLearning.Tests/Helpers/StepEngineTests.cs`
- `tests/MathLearning.Tests/Content/MathContentSanitizerTests.cs`
- `tests/MathLearning.Tests/Helpers/TranslationHelperTests.cs`
- `tests/MathLearning.Tests/Domain/QuestionEntityTests.cs`

## Existing tests expanded

- `tests/MathLearning.Tests/Services/DatabaseSchemaVersionGuardTests.cs`
- `tests/MathLearning.Tests/Services/WeaknessScoringTests.cs`

The touched suites now contain more than 110 test methods and more than 220 expanded xUnit cases, focused on boundaries, invariants, privacy, deterministic concurrency and content preservation.

## Coverage added

### Identity mapping

- deterministic integer and string identity mapping;
- exact stable hash-derived mapping regression;
- 1,000-value isolation checks;
- GUID round-trip, case behavior and invalid inputs.

### Idempotency observability

- zero state, all categories, normalized/sorted rows and reset;
- 20,000 parallel increments with exact totals;
- route resolution and fallback behavior;
- privacy-safe logging without raw user/operation identifiers;
- anonymous/learner/admin endpoint boundaries and exact policy metadata.

### Startup/schema state

- configured modes and environment defaults;
- invalid configuration fallback;
- schema status factories/counts and state replacement;
- deployment/local mismatch guidance, placeholders and inner exceptions.

### Weakness math

- level thresholds, accuracy rounding, attempt/recency factors;
- score/confidence ordering and clamps;
- slow-solve boundaries and timing preconditions;
- P95 empty, negative, single and nearest-rank behavior.

### Math content and explanations

- inline math preservation, mixed normalization and idempotence;
- real `/api/quiz/questions` JSON preservation for text/options/hints/explanation;
- stored-step precedence, translations, arithmetic/equation generation and fallbacks;
- translation hierarchy and accessibility semantics;
- content sanitization for scripts, event attributes, unsafe URL schemes, HTML/plain modes, malformed math and generated semantics.

### Question domain

- required text and difficulty bounds;
- multiple-choice/open-answer invariants;
- correct option synchronization and ordering;
- hint bounds, step ordering, publish/version state and soft-delete/restore.

## Runtime defects fixed

1. `InlineLatexFormatter` previously discarded existing `$...$` matches while splitting text. It now copies existing inline math exactly and normalizes only plain segments.
2. `MathContentSanitizer` previously removed only quoted event-handler values and did not remove unsafe URL schemes. It now handles quoted/unquoted event attributes and unsafe `href`/`src` values while preserving safe HTTP(S) links.

## Runtime files changed

- `src/MathLearning.Application/Helpers/InlineLatexFormatter.cs`
- `src/MathLearning.Application/Content/MathContentSanitizer.cs`

## Static validation performed

- expected results were checked against current production branches;
- test enum values were aligned to actual enum members;
- sequence assertions were materialized to avoid overload ambiguity;
- HTTP regression uses a dedicated topic/subtopic and one-question filter;
- concurrency test uses exact totals and no sleeps;
- privacy/auth tests are separated from direct service tests;
- central queue and mistake ledger were reconciled.

## Validation not run

No local checkout/.NET SDK or completed CI status is available in this connector session. No test or build pass is claimed.

Focused validation required:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "FullyQualifiedName~UserIdGuidMapperTests|FullyQualifiedName~IdempotencyObservabilityServiceTests|FullyQualifiedName~IdempotencyObservabilityAuthorizationTests|FullyQualifiedName~DatabaseSchemaVersionGuardTests|FullyQualifiedName~WeaknessScoringTests|FullyQualifiedName~InlineLatexFormatterTests|FullyQualifiedName~InlineLatexEndpointContractTests|FullyQualifiedName~StepEngineTests|FullyQualifiedName~MathContentSanitizerTests|FullyQualifiedName~TranslationHelperTests|FullyQualifiedName~QuestionEntityTests"

dotnet build MathLearning.slnx -c Release
```

Broader regression command:

```text
dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj --filter "Translation|QuestionAuthoring|Explanation|QuizEndpoint|Srs|IdempotencyObservability|DatabaseSchema|Weakness"
```

## Residual risk

- all new/expanded tests remain execution-unvalidated;
- regex-based HTML sanitization is stronger but is not equivalent to a mature allowlist parser;
- extremely large authored integers in `StepEngine` are not exhaustively bounded;
- PostgreSQL provider behavior remains BACKEND-TEST-032;
- numeric line/branch baseline still requires the first successful CI coverage artifact.

## Completion

88%

## Key commits

- `aff4ff3f2baef65dcc52d119c8c125cef2633838` — evidence start
- `29edc36f1693f256e731c916f1fee0bd7f7a7992` — identity mapping tests
- `19b76b64343bb213aaff6a2f29e028266b8e5a2f` — observability service tests
- `5c975531dbdf72725bd8cd278771eb6dd8e1f562` — observability endpoint tests
- `646180f717b6361bab840170df2a557ef3d3fd68` — schema guard tests
- `d870ccf9f6b86eee459dd610b5f9614284d55698` — weakness scoring tests
- `5f6561f845eea92ff9ee5fdb55362e6122ff8348` — inline math preservation fix
- `29f82fea52dcd680790f6cf02cb38a74f0522ef0` — inline math helper tests
- `23e1aae3159b114b2f461a1ec042687745f7dd92` — inline math HTTP test
- `6da2703dec7ede176edba13b84de08e549d5f24d` — StepEngine tests
- `e6ae11d83db2be19e61970ba90ec68f7e66f23b3` — sanitizer runtime hardening
- `93b58d47dc5dfe420236d84d2d659cbf42e6dae4` — sanitizer tests
- `e3be575941853c04f051d39572cdd252024e7f31` — translation tests
- `829a4078cad485d4ca2bd35e098662f0c4e44410` — question domain tests
- `fa7ca3cd4ae963839c1193828ecaf38a1183c42f` — mistake ledger
- `2cd1bd93340d28cc48fa3c8ad5efb7f098cbef2f` — central queue

## Cross-repo sync

No request/response shape was intentionally changed. The inline math fix restores content the existing contract already intended to return. Mobile docs touched: none.
