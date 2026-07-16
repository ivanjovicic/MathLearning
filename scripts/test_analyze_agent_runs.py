from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
import analyze_agent_runs as analyzer


V2 = """Evidence format: v2
Run mode: known-fix
Elapsed time: 5m 0s
Files inspected: 4
Files changed: 2
Searches: 1
Completion %: 95
Waste: none
"""


class AnalyzeAgentRunsTests(unittest.TestCase):
    def test_clean_v2_has_no_regression(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "run.md"
            path.write_text(V2, encoding="utf-8")
            item = analyzer.metric(path, root)
            self.assertEqual("v2", item.format)
            self.assertEqual([], analyzer.regression_findings([item]))

    def test_flags_mixed_lane_and_unknown_elapsed(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "run.md"
            path.write_text(V2.replace("known-fix", "implementation + migration").replace("5m 0s", "unknown-not-recorded"), encoding="utf-8")
            findings = analyzer.regression_findings([analyzer.metric(path, root)])
            self.assertTrue(any("mixed run lane" in item for item in findings), findings)
            self.assertTrue(any("elapsed time" in item for item in findings), findings)

    def test_summary_counts_long_legacy(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            path = root / "legacy.md"
            path.write_text("Run mode: audit\nElapsed time: unknown-not-recorded\n" + "x\n" * 130, encoding="utf-8")
            summary = analyzer.summarize([analyzer.metric(path, root)])
            self.assertEqual(1, summary["legacy"])
            self.assertEqual(1, summary["over_120_lines"])
            self.assertEqual(1, summary["unknown_elapsed"])


if __name__ == "__main__":
    unittest.main()
