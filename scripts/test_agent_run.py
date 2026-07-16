from __future__ import annotations

import json
import tempfile
import unittest
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
import agent_run


class AgentRunTests(unittest.TestCase):
    def setUp(self) -> None:
        self.index = {
            "version": 1,
            "default": {"mistakes": ["M-DEFAULT"], "reads": ["A"], "proof": ["P"]},
            "areas": {"auth": {"mistakes": ["M-AUTH"], "reads": ["B"], "proof": ["Q"]}},
        }

    def test_merge_plan_deduplicates_and_routes(self) -> None:
        plan = agent_run.merge_plan(self.index, ["auth"])
        self.assertEqual(["M-DEFAULT", "M-AUTH"], plan["mistakes"])
        self.assertEqual(["A", "B"], plan["reads"])

    def test_unknown_area_is_rejected(self) -> None:
        with self.assertRaises(ValueError):
            agent_run.merge_plan(self.index, ["missing"])

    def test_plan_includes_limits(self) -> None:
        text = agent_run.plan_text(lane="known-fix", budget="micro", areas=["auth"], index=self.index)
        self.assertIn("8 minutes", text)
        self.assertIn("changed=2", text)
        self.assertIn("M-AUTH", text)

    def test_start_and_finish_compact_log(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            old_root = agent_run.ROOT
            old_index = agent_run.INDEX_PATH
            try:
                agent_run.ROOT = root
                index_path = root / "docs/ai/learning/MISTAKE_INDEX.json"
                index_path.parent.mkdir(parents=True)
                index_path.write_text(json.dumps(self.index), encoding="utf-8")
                agent_run.INDEX_PATH = index_path
                output = root / ".ai/runs/test.md"
                code = agent_run.main([
                    "start", "--prompt-id", "TEST-001", "--area", "auth", "--output", str(output),
                    "--lane", "known-fix", "--budget", "micro"
                ])
                self.assertEqual(0, code)
                self.assertIn("Evidence format: v2", output.read_text(encoding="utf-8"))
                code = agent_run.main([
                    "finish", str(output), "--completion", "95", "--inspected", "4", "--changed", "2",
                    "--searches", "1", "--validation-runs", "1", "--outcome", "fixed",
                    "--changed-path", "src/A.cs", "--validation", "focused test passed",
                    "--branch-pr", "agent/test / PR #1"
                ])
                self.assertEqual(0, code)
                text = output.read_text(encoding="utf-8")
                self.assertIn("Files changed: 2", text)
                self.assertIn("Completion %: 95", text)
                self.assertIn("Validation run: focused test passed", text)
                self.assertNotIn("Completed at UTC: open", text)
            finally:
                agent_run.ROOT = old_root
                agent_run.INDEX_PATH = old_index

    def test_finish_rejects_legacy_log(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / "legacy.md"
            path.write_text("Prompt ID: X\n", encoding="utf-8")
            args = agent_run.build_parser().parse_args([
                "finish", str(path), "--completion", "50", "--inspected", "1", "--changed", "1",
                "--searches", "0", "--validation-runs", "0"
            ])
            with self.assertRaises(ValueError):
                agent_run.finish_log(args)


if __name__ == "__main__":
    unittest.main()
