from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
import check_documentation_health as docs_health
import validate_agent_system as validator


class AgentSystemValidatorTests(unittest.TestCase):
    def build_minimal(self, root: Path) -> None:
        for relative in validator.REQUIRED_PATHS:
            path = root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("placeholder\n", encoding="utf-8")
        ledger_ids = [
            "BACKEND-MISTAKE-EVIDENCE-001", "BACKEND-MISTAKE-VALIDATION-001",
            "BACKEND-MISTAKE-PROCESS-001", "BACKEND-MISTAKE-PROCESS-002",
            "BACKEND-MISTAKE-SCOPE-001", "BACKEND-MISTAKE-CI-001",
        ]
        (root / "docs/ai/learning/MISTAKE_LEDGER.md").write_text("\n".join(ledger_ids), encoding="utf-8")
        (root / "docs/ai/learning/MISTAKE_INDEX.json").write_text(json.dumps({
            "version": 1, "areas": {"x": {"mistakes": ledger_ids}},
        }), encoding="utf-8")
        for relative, references in validator.REQUIRED_REFERENCES.items():
            path = root / relative
            content = "\n".join(references)
            if relative == "AGENTS.md":
                content = "# MathLearning Backend\n" + content
            path.write_text(content, encoding="utf-8")
        manifest_docs = []
        for relative in (
            "AGENTS.md", "docs/DOCUMENTATION_SYSTEM.md", "docs/DOCS_INDEX.md",
            ".ai/README.md", ".ai/SOURCE_OF_TRUTH.md",
        ):
            manifest_docs.append({
                "path": relative,
                "title": relative,
                "class": "rule",
                "owner": "test",
                "purpose": "test",
                "review_days": 30,
                "last_verified": "2026-07-17",
                "source_globs": ["src/**"],
                "impact": "required",
            })
        manifest = {"version": 1, "documents": manifest_docs}
        (root / "docs/DOCS_MANIFEST.json").write_text(json.dumps(manifest), encoding="utf-8")
        (root / "docs/DOCS_REGISTRY.md").write_text(docs_health.registry_text(manifest_docs), encoding="utf-8")

    def test_complete_wiring_passes(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.build_minimal(root)
            failures = [item for item in validator.validate(root) if item.severity == "FAIL"]
            self.assertFalse(failures, failures)

    def test_unknown_mistake_id_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.build_minimal(root)
            index = root / "docs/ai/learning/MISTAKE_INDEX.json"
            index.write_text(json.dumps({"version": 1, "areas": {"x": {"mistakes": ["BACKEND-MISTAKE-UNKNOWN-999"]}}}), encoding="utf-8")
            findings = validator.validate(root)
            self.assertTrue(any("unknown ID" in item.message for item in findings), findings)

    def test_forbidden_slow_default_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.build_minimal(root)
            path = root / ".ai/README.md"
            path.write_text(path.read_text() + "Read the whole mistake ledger", encoding="utf-8")
            findings = validator.validate(root)
            self.assertTrue(any("slow default" in item.message for item in findings), findings)

    def test_broken_link_fails(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.build_minimal(root)
            path = root / ".ai/README.md"
            path.write_text(path.read_text() + "\n[bad](missing.md)\n", encoding="utf-8")
            findings = validator.validate(root)
            self.assertTrue(any("broken relative link" in item.message for item in findings), findings)

    def test_conflict_marker_fails_through_docs_health(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            self.build_minimal(root)
            path = root / "AGENTS.md"
            path.write_text(path.read_text() + "\n<<<<<<< HEAD\n", encoding="utf-8")
            findings = validator.validate(root)
            self.assertTrue(any("merge-conflict marker" in item.message for item in findings), findings)


if __name__ == "__main__":
    unittest.main()
