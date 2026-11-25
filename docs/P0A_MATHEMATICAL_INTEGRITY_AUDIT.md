# P0-A mathematical integrity audit

Status date: 2026-11-25. The repository base reviewed for this work was
`89ecdaae32924efb19f3d7eec61bbc488fa9f2df`; implementation and test evidence
will be tied to the resulting PR head SHA. Mobile source was read-only at
`a82ba7026d06db2151aeb7e8c92a0efea17bb4be`. The visual proposal is not teacher
approval.

## Mathematical and explanation paths

`StepEngine.GetSteps` returns persisted localized steps first, then an existing
explanation fallback, then generated steps
([StepEngine.cs](../src/MathLearning.Application/Helpers/StepEngine.cs#L13)). Its
expression regexes are unanchored (`GenerateFromQuestion`, lines 96–125), and
Serbian small multiplication says “add the first factor, the second factor
times” (`ForMultiplicationSr`, lines 292–305). For `Koliko je 7 × 8?`, a direct
StepEngine call therefore describes eight groups of seven, while the proposed
reviewed array convention is seven rows of eight. This is equal arithmetically,
but the explanation's group orientation differs. An unanchored match also
allows a decimal/negative expression to match only a positive substring.

`LegacyStepExplanationAdapter.GetSteps` takes persisted authored steps first;
otherwise it asks `MathReasoningGraphEngine`, then falls back to StepEngine when
the graph is unknown (`LegacyStepExplanationAdapter.cs`, lines 26–55). The
graph arithmetic regex is anchored (`MathReasoningGraphEngine.cs`, lines
127–129, 444), so a bare `7 × 8` reaches its direct arithmetic graph while a
natural-language prompt does not. That confirms these paths are not
interchangeable. No global multiplication convention or legacy grading behavior
was changed pending teacher direction.

At answer time, `/api/quiz/start` captures question snapshots; settlement
applies that snapshot before grading and reads the same snapshot for visual
feedback (`QuizEndpoints.cs`, lines 37–77, 325–331). The existing P0-PR #66
issued-version behavior is extended, not reimplemented. The visual service
accepts only a matching published `QuestionVersion` snapshot and approved hash
(`IssuedMathVisualService.cs`, lines 20–105); the signed snapshot is returned
only after settlement (lines 107–155). Old ID-only manifests have no snapshot
and therefore produce no aid. Practice has no MathVisual issuer. Offline
bundles exclude configured pack subtopics; previously downloaded bundles
cannot be recalled by this server change.

## Required prompt and factor matrix

The following are checked by `MathVisualContentPolicyTests` against the ordered
prompt parser; the 64-pair test exhaustively validates geometry over rows and
columns 2–9 and enumerates 36 unordered commutative families. The two factor
orders remain distinct plans.

| Input | MathVisual outcome | Evidence / reason |
|---|---|---|
| `7 × 8` | Accept as ordered 7 rows × 8 columns | Prompt parser test; 64 ordered-pair validation |
| `Koliko je 7 × 8?` | Accept as 7 × 8 | Prompt parser test; natural-language adapter path differs from graph path |
| `7 × 8 = ?` | Accept as 7 × 8 | Prompt parser test |
| `8 × 7` | Accept as a separate 8 × 7 plan | Ordered factors preserved; transpose mismatch test |
| `2 × 2` | Accept direct array; no five-plus-remainder split | Exhaustive 64-pair geometry validation |
| `5 × 8` | Accept direct 5-row array | Exhaustive 64-pair geometry validation |
| `6 × 7` | Accept as 5 + 1 rows, each 7 elements | Exhaustive 64-pair geometry validation |
| `9 × 9` | Accept as 5 + 4 rows, each 9 elements | Exhaustive 64-pair geometry validation |
| `? × 8 = 56` | Reject; no inferred answer geometry | Unsupported prompt test |
| `7 × ? = 56` | Reject; no inferred answer geometry | Unsupported prompt test |
| `56 ÷ 8` | Reject for this multiplication visual | Unsupported prompt test; graph/StepEngine may still explain legacy division |
| `0 × 8` | Reject | Factors must be in 2–9 |
| `-7 × 8` | Reject | Unsupported factor range/shape |
| `7.5 × 8` | Reject | Unsupported factor type/shape |

The visual model also rejects unknown templates (including `double`, `neighbor`,
`commute`, and `missingFactor`), transposed factor plans, nonpartitioning groups,
false arithmetic equations, PRE hints/answer-bearing semantics, markup, scripts,
executable URLs and external-image Markdown. Explanatory natural language still
requires teacher approval for the exact content hash; the validator is not a
natural-language mathematics prover.

## Publication and issued-version controls

`MathQuestionAuthoringService.RunPipelineAsync` adds the policy's errors to the
existing publish summary and `PublishCoreAsync` reruns it at publish time
(`MathQuestionAuthoringService.cs`, lines 256–287, 504–524). Content edits alter
the hash and lose approval. The policy requires a server-designated subtopic,
pack id, exact server allowlisted hash, Serbian locale, authored steps and an
ordered product plan; configuration ships empty and the API feature ships
disabled. Startup seeding now requires that policy and skips its legacy
playable-question promotion for designated pack subtopics
(`DbSeeder.cs`, lines 10, 990–1020; API and Admin both inject the same policy).
This closes the direct seeder republish path; the negative seeder test covers
the previously demonstrated bypass.

The API response is a separate DTO and omits the internal HMAC tag. HMAC binds
the full issued snapshot to quiz id and user id. No database schema migration is
needed: plan metadata lives in the existing serialized authoring snapshot and
MathVisual in the issued session JSON. The additive nullable response property
is omitted when absent; `openapi.yaml` now matches the actual `totalXp` field.
Online quiz issue routes force Serbian for configured pack subtopics and omit
their legacy hints and semantic-alt fields; answer feedback uses the issued
visual locale when present. The issuer also compares live type, options,
semantics, hint fields and step text/hints with the exact published snapshot
before attaching an aid.

## Known boundaries and residual risks

- No teacher-approved hash or content exists in runtime configuration. The
  proposed array convention and authored content are fixtures only.
- Admin's legacy editor does not author MathVisual metadata. A designated pack
  must be authored through the validated draft/publish flow; any direct content
  creation path stays draft and the seeder cannot promote it.
- `DbSeeder` is guarded for both API and Admin startup; a new runtime seeder
  entry point must also receive the required policy.
- `PlayableQuestionQuery` still serves rows that were already published before
  this guard, including rows in a future designated pack subtopic. The new
  seeder skip blocks future promotion; it does not quarantine existing rows or
  validate their historical `QuestionVersion` hash at selection time. Before
  configuring any `PackSubtopicIds`, inventory published rows, current versions
  and drafts in those subtopics, then quarantine any row without the exact
  teacher-approved hash. This is a release blocker; no production inventory or
  data change was made in this work.
- Question/option translations are stored separately from the serialized
  authoring request and are not covered by its reviewed content hash. This
  first contract supports Serbian only. Before configuring a pack subtopic,
  inventory or disable its non-Serbian translated prompts, options, hints,
  explanations and semantic descriptions; otherwise the legacy question DTO
  in practice/SRS and offline legacy surfaces can still serve that localized
  content without a reviewed visual. Online quiz routes use the Serbian base
  text for designated subtopics.
- Legacy online questions can still receive generated text from the existing
  explanation engines. Resolving the multiplication orientation is deliberately
  deferred until teacher guidance because equal products do not establish
  didactic equivalence.
- Offline bundle filtering affects new downloads only; content already cached by
  mobile clients cannot be retracted. Legacy offline/batch settlement, practice
  and ID-only sessions do not generate MathVisual.
- HMAC key rotation invalidates pending stored tags, causing the service to
  omit the aid safely. Rotate only with a deployment window or key-version
  support if pending sessions must preserve visuals.
- This feature is additive and old clients may ignore it, but mobile #1015 must
  ignore unknown schema/template versions and render only the typed allowlist.

## Mobile follow-up

- **#1015 MathBlocks renderer:** parse optional `mathVisual` only from settled
  answer responses; validate schema/template version, integer bounds, ordered
  factors, group sums and step ordering; render rows/columns from typed values;
  ignore unknown versions/templates; never infer strategy from question text or
  render HTML/JS/SVG/external image content; keep PRE screen free of geometry and
  accessibility text that encodes the answer.
- **#987 server-confirmed feedback:** consume aid only after the server's answer
  settlement; key it to quiz/question/version; preserve the exact replayed body;
  do not generate a visual for offline, practice or legacy ID-only responses;
  verify old clients work when the optional field is absent and that duplicate
  submit does not replay XP side effects.

Teacher review of the content proposal remains required before pedagogical GO.
