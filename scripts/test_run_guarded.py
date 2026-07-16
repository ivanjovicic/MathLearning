#!/usr/bin/env python3
from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
import run_guarded


class RunGuardedTests(unittest.TestCase):
    def python_command(self, source: str) -> list[str]:
        return [sys.executable, "-S", "-c", source]

    def test_pass_and_json(self):
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "result.json"
            result = run_guarded.run_guarded(
                self.python_command("print('ok')"),
                timeout_seconds=2,
                idle_timeout_seconds=1,
                json_output=output,
            )
            self.assertEqual(0, result.exit_code)
            self.assertEqual("completed", result.outcome)
            self.assertEqual(0, json.loads(output.read_text(encoding="utf-8"))["exit_code"])

    def test_propagates_nonzero_exit(self):
        result = run_guarded.run_guarded(
            self.python_command("raise SystemExit(7)"),
            timeout_seconds=2,
            idle_timeout_seconds=1,
        )
        self.assertEqual(7, result.exit_code)

    def test_wall_timeout_kills_command(self):
        result = run_guarded.run_guarded(
            self.python_command("import time; print('start', flush=True); time.sleep(2)"),
            timeout_seconds=1,
            idle_timeout_seconds=1,
        )
        self.assertEqual(124, result.exit_code)
        self.assertEqual("wall-timeout", result.outcome)

    def test_idle_timeout_kills_silent_command(self):
        result = run_guarded.run_guarded(
            self.python_command("import time; time.sleep(2)"),
            timeout_seconds=2,
            idle_timeout_seconds=1,
        )
        self.assertEqual(125, result.exit_code)
        self.assertEqual("idle-timeout", result.outcome)

    def test_rejects_chaining_token(self):
        with self.assertRaises(ValueError):
            run_guarded.run_guarded(
                [*self.python_command("print(1)"), "&&", "echo"],
                timeout_seconds=2,
                idle_timeout_seconds=1,
            )

    def test_rejects_excessive_limit(self):
        with self.assertRaises(ValueError):
            run_guarded.run_guarded(
                self.python_command("print(1)"),
                timeout_seconds=301,
                idle_timeout_seconds=1,
            )


if __name__ == "__main__":
    unittest.main()
