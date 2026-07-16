#!/usr/bin/env python3
from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
import validate_agent_prompt as validator


VALID_PROMPT = """# BACKEND-TEST-999 — Verify one backend behavior

Prompt contract: v2
Repository: ivanjovicic/MathLearning
Prompt ID: BACKEND-TEST-999
Queue: docs/prompt_queues/example.md
Run lane: known-fix
Token budget: low
Timebox: 15 minutes
Task: Fix one bounded endpoint defect.
Source of truth: current code and focused tests
Interpretation before work: preserve the mobile contract and change one owner.
Ambiguity rule: stop on auth, persistence or contract ambiguity.
Risk/ownership model: the application service is authoritative; endpoint formatting is excluded.
Failure-mode matrix:
- normal request returns the documented response;
- duplicate retry does not apply the mutation twice.
Execution packet:
- Initial reads: prompt, owner, test; maximum 6.
- Search budget: maximum 2.
- First hypothesis/falsifier: service misses the idempotency lookup.
- Expected changed files: service and focused test; maximum 3.
- Validation target: guarded focused dotnet test within 180 seconds.
Owned paths:
- src/MathLearning.Application/Example.cs
- tests/MathLearning.Tests/ExampleTests.cs
Avoid paths:
- migrations and unrelated endpoints
Documentation impact: none - no durable contract change.
Acceptance criteria:
- the normal request succeeds;
- the duplicate request replays without a second mutation;
- only owned paths change and focused proof executes.
Proof required: focused regression test plus changed-file review.
Validation:
```text
python scripts/run_guarded.py --timeout-seconds 180 -- dotnet test tests/MathLearning.Tests/MathLearning.Tests.csproj
```
Completion gate: proof passes, evidence is updated and delivery state is honest.
Stop conditions: second subsystem, second falsified hypothesis, unavailable proof or timebox.
Evidence: .ai/runs/<date>-BACKEND-TEST-999-evidence.md
"""


def validate(text: str, *, strict: bool = True) -> list[validator.Finding]:
    with tempfile.TemporaryDirectory() as temp:
        path = Path(temp) / "prompt.md"
        path.write_text(text, encoding="utf-8")
        return validator.validate_files([path], strict=strict)


class PromptValidatorTests(unittest.TestCase):
    def test_accepts_valid_backend_v2_prompt(self) -> None:
        findings = validate(VALID_PROMPT)
        self.assertFalse(any(item.severity == "FAIL" for item in findings), findings)

    def test_rejects_missing_required_field(self) -> None:
        findings = validate(VALID_PROMPT.replace("Owned paths:\n", "Paths:\n"))
        self.assertTrue(any("Owned paths" in item.message for item in findings), findings)

    def test_rejects_flutter_command(self) -> None:
        findings = validate(VALID_PROMPT.replace("focused regression test", "flutter test plus focused regression test"))
        self.assertTrue(any("Flutter-specific" in item.message for item in findings), findings)

    def test_rejects_timebox_above_thirty_minutes(self) -> None:
        findings = validate(VALID_PROMPT.replace("Timebox: 15 minutes", "Timebox: 45 minutes"))
        self.assertTrue(any("must not exceed 30" in item.message for item in findings), findings)

    def test_accepts_repository_task_template_placeholders(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            old_root = validator.ROOT
            try:
                validator.ROOT = root
                path = root / "docs/ai/TASK_TEMPLATE.md"
                path.parent.mkdir(parents=True)
                template = VALID_PROMPT.replace("Run lane: known-fix", "Run lane: known-fix | investigation")
                template = template.replace("Token budget: low", "Token budget: low | medium | high")
                path.write_text(template, encoding="utf-8")
                findings = validator.validate_files([path])
                self.assertFalse(any(item.severity == "FAIL" for item in findings), findings)
            finally:
                validator.ROOT = old_root

    def test_admission_requires_deduplication_details(self) -> None:
        admitted = VALID_PROMPT.replace(
            "Prompt contract: v2",
            """Prompt contract: v2
Prompt admission: v3
Problem evidence:
- current service applies the same operation twice;
- expected invariant is exactly-once settlement.
Deduplication check:
- searched one queue.
Priority rationale: P0 because duplicate settlement changes authenticated economy state.
Dependencies/collisions:
- no prerequisite because the ledger exists;
- avoid the mobile implementation owner.
Owner boundary:
- backend ledger owns settlement;
- mobile retry UI is excluded.
Queue placement: docs/prompt_queues/example.md after the current P0 blocker.""",
        )
        findings = validate(admitted)
        self.assertTrue(any("Deduplication check needs at least 3" in item.message for item in findings), findings)


if __name__ == "__main__":
    unittest.main()
