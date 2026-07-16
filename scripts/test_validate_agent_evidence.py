from __future__ import annotations

import subprocess
import tempfile
import unittest
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
import validate_agent_evidence as validator


V2_LOG = """# TEST-001 Evidence

Evidence format: v2
Prompt ID: TEST-001
Queue: user-assigned
Agent/tool: test
Model provider: OpenAI
Model name/id: test
Client/IDE: test
Run mode: known-fix
Token budget: micro
Started at UTC: 2026-07-17T10:00:00Z
Completed at UTC: 2026-07-17T10:05:00Z
Elapsed time: 5m 0s
Relevant prior mistakes read: BACKEND-MISTAKE-EVIDENCE-001
How this run avoids prior mistakes: use compact evidence
Owner/hypothesis: owner
Files inspected: 4
Files changed: 2
Searches: 1
Validation runs: 1
Failed retries: 0

## Outcome
- fixed

## Changed paths
- scripts/x.py

## Validation
Validation run: focused test passed
Validation not run: none

## Exceptions and learning
Mistakes observed: none
Waste: none
Missed: none
Follow-up: none
Residual risk: none
Documentation impact: updated docs
Cross-repo impact: no

## Delivery
State: Done
Branch/PR: main
Commit SHA: self
Completion %: 100
"""


def init_repo(root: Path) -> str:
    subprocess.run(["git", "init"], cwd=root, check=True, capture_output=True)
    subprocess.run(["git", "config", "user.email", "test@example.com"], cwd=root, check=True)
    subprocess.run(["git", "config", "user.name", "Test"], cwd=root, check=True)
    (root / "docs/ai/learning").mkdir(parents=True)
    (root / "docs/prompt_queues").mkdir(parents=True)
    (root / ".ai/runs").mkdir(parents=True)
    (root / "docs/ai/learning/MISTAKE_LEDGER.md").write_text(
        "BACKEND-MISTAKE-EVIDENCE-001\n", encoding="utf-8"
    )
    (root / "docs/prompt_queues/q.md").write_text("# queue\n", encoding="utf-8")
    subprocess.run(["git", "add", "."], cwd=root, check=True)
    subprocess.run(["git", "commit", "-m", "base"], cwd=root, check=True, capture_output=True)
    return subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, check=True, text=True, capture_output=True).stdout.strip()


class EvidenceValidatorTests(unittest.TestCase):
    def test_v2_log_accepts_self_and_budget(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            init_repo(root)
            path = root / ".ai/runs/test.md"
            path.write_text(V2_LOG, encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=root, check=True)
            subprocess.run(["git", "commit", "-m", "log"], cwd=root, check=True, capture_output=True)
            findings = validator.validate_run_log(
                path, {"BACKEND-MISTAKE-EVIDENCE-001"}, {path}, verify_git=True, root=root
            )
            self.assertFalse([f for f in findings if f.severity == "FAIL"], findings)

    def test_v2_rejects_mixed_lane(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "run.md"
            path.write_text(V2_LOG.replace("Run mode: known-fix", "Run mode: implementation + migration"), encoding="utf-8")
            findings = validator.validate_v2_log(path, path.read_text(), {"BACKEND-MISTAKE-EVIDENCE-001"}, False, Path(temp))
            self.assertTrue(any("one lane" in f.message for f in findings if f.severity == "FAIL"), findings)

    def test_v2_budget_breach_caps_completion(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "run.md"
            text = V2_LOG.replace("Files changed: 2", "Files changed: 7")
            path.write_text(text, encoding="utf-8")
            findings = validator.validate_v2_log(path, text, {"BACKEND-MISTAKE-EVIDENCE-001"}, False, Path(temp))
            self.assertTrue(any("budget breach" in f.message for f in findings if f.severity == "FAIL"), findings)

    def test_failed_validation_cannot_be_done(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "run.md"
            text = V2_LOG.replace("focused test passed", "focused test failed")
            path.write_text(text, encoding="utf-8")
            findings = validator.validate_v2_log(path, text, {"BACKEND-MISTAKE-EVIDENCE-001"}, False, Path(temp))
            self.assertTrue(any("failed validation" in f.message for f in findings if f.severity == "FAIL"), findings)

    def test_changed_mode_ignores_untouched_legacy_debt(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            init_repo(root)
            old_log = root / ".ai/runs/old.md"
            old_log.write_text("Prompt ID: OLD\n", encoding="utf-8")
            queue = root / "docs/prompt_queues/q.md"
            queue.write_text("# queue\n| OLD | Done 100% | no evidence |\n", encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=root, check=True)
            subprocess.run(["git", "commit", "-m", "legacy"], cwd=root, check=True, capture_output=True)
            changed_base = subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, check=True, text=True, capture_output=True).stdout.strip()
            new_log = root / ".ai/runs/new.md"
            new_log.write_text(V2_LOG, encoding="utf-8")
            queue.write_text(queue.read_text(encoding="utf-8") + "| TEST-001 | Done 100% — Run log: `.ai/runs/new.md`; Validation: focused pass; Residual risk: none; Commit: self |\n", encoding="utf-8")
            subprocess.run(["git", "add", "."], cwd=root, check=True)
            subprocess.run(["git", "commit", "-m", "new"], cwd=root, check=True, capture_output=True)
            findings = validator.validate_changed(changed_base, verify_git=True, root=root)
            failures = [f for f in findings if f.severity == "FAIL"]
            self.assertFalse(failures, failures)
            self.assertFalse(any(f.path == old_log for f in findings), findings)

    def test_changed_queue_row_requires_compact_fields(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "q.md"
            path.write_text("| X | Done 100% |\n", encoding="utf-8")
            findings, _ = validator.validate_queue_row(path, 1, path.read_text(), root)
            self.assertTrue(any("compact evidence fields" in f.message for f in findings), findings)


if __name__ == "__main__":
    unittest.main()
