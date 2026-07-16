#!/usr/bin/env python3
from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
import validate_agent_system as validator


class AgentSystemValidatorTests(unittest.TestCase):
    def make_root(self) -> Path:
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        root = Path(temp.name)
        for relative in validator.REQUIRED_PATHS:
            path = root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("placeholder\n", encoding="utf-8")
        (root / "AGENTS.md").write_text(
            "# MathLearning Backend - AI Agent Rulebook\n"
            ".ai/README.md .ai/VALIDATION_SELECTOR.md .ai/PROMPT_LINT_CHECKLIST.md "
            "docs/AGENT_COMMAND_PLAYBOOK.md docs/prompt_queues/README.md\n",
            encoding="utf-8",
        )
        (root / "docs/DOCS_INDEX.md").write_text(
            ".ai/README.md .ai/SOURCE_OF_TRUTH.md .ai/TOKEN_BUDGETS.md "
            ".ai/VALIDATION_SELECTOR.md docs/AGENT_COMMAND_PLAYBOOK.md docs/prompt_queues/README.md\n",
            encoding="utf-8",
        )
        (root / ".github/workflows/agent-system-validation.yml").write_text(
            "scripts/test_run_guarded.py scripts/test_validate_agent_prompt.py "
            "scripts/validate_agent_system.py\n",
            encoding="utf-8",
        )
        return root

    def test_accepts_complete_wiring(self) -> None:
        root = self.make_root()
        self.assertEqual([], validator.validate(root))

    def test_rejects_missing_required_file(self) -> None:
        root = self.make_root()
        (root / ".ai/README.md").unlink()
        findings = validator.validate(root)
        self.assertTrue(any("required agent-system file is missing" in item.message for item in findings), findings)

    def test_rejects_broken_relative_link(self) -> None:
        root = self.make_root()
        (root / ".ai/README.md").write_text("[Missing](missing.md)\n", encoding="utf-8")
        findings = validator.validate(root)
        self.assertTrue(any("broken relative link" in item.message for item in findings), findings)

    def test_rejects_flutter_runtime_command(self) -> None:
        root = self.make_root()
        (root / ".ai/VALIDATION_SELECTOR.md").write_text("flutter test test/example.dart\n", encoding="utf-8")
        findings = validator.validate(root)
        self.assertTrue(any("Flutter-only runtime command" in item.message for item in findings), findings)


if __name__ == "__main__":
    unittest.main()
