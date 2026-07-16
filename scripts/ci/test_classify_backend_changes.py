from __future__ import annotations

import tempfile
import unittest
from pathlib import Path
import sys

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
import classify_backend_changes as classifier


class ClassifyBackendChangesTests(unittest.TestCase):
    def test_docs_and_agent_tooling_skip_database_suite(self) -> None:
        required, matched = classifier.requires_database_validation([
            "docs/AGENT_RUN_LOG_ENFORCEMENT.md",
            ".ai/RUN_LOG_TEMPLATE.md",
            "scripts/agent_run.py",
            ".github/workflows/database-validation.yml",
        ])
        self.assertFalse(required)
        self.assertEqual([], matched)

    def test_runtime_source_requires_database_suite(self) -> None:
        required, matched = classifier.requires_database_validation(["src/MathLearning.Api/Program.cs"])
        self.assertTrue(required)
        self.assertEqual(["src/MathLearning.Api/Program.cs"], matched)

    def test_tests_and_migrations_require_database_suite(self) -> None:
        required, matched = classifier.requires_database_validation([
            "tests/MathLearning.Tests/X.cs",
            "src/MathLearning.Infrastructure/Migrations/Api/M.cs",
        ])
        self.assertTrue(required)
        self.assertEqual(2, len(matched))

    def test_solution_and_build_files_require_database_suite(self) -> None:
        required, _ = classifier.requires_database_validation(["MathLearning.slnx", "Directory.Build.props"])
        self.assertTrue(required)

    def test_force_writes_output(self) -> None:
        with tempfile.TemporaryDirectory() as temp:
            output = Path(temp) / "out"
            code = classifier.main(["--force", "--github-output", str(output)])
            self.assertEqual(0, code)
            self.assertIn("database_validation=true", output.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
