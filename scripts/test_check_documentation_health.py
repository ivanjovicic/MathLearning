from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPT_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(SCRIPT_DIR))
import check_documentation_health as health


class DocumentationHealthTests(unittest.TestCase):
    def build_root(self) -> Path:
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        root = Path(temp.name)
        (root / "docs").mkdir(parents=True)
        (root / "AGENTS.md").write_text("# Rulebook\n", encoding="utf-8")
        (root / "docs/DOCUMENTATION_SYSTEM.md").write_text("# Docs\n", encoding="utf-8")
        (root / "docs/DOCS_INDEX.md").write_text("[Rules](../AGENTS.md)\n[Docs](DOCUMENTATION_SYSTEM.md)\n", encoding="utf-8")
        documents = [
            self.entry("AGENTS.md", ["src/**"]),
            self.entry("docs/DOCUMENTATION_SYSTEM.md", ["docs/DOCS_MANIFEST.json"]),
            self.entry("docs/DOCS_INDEX.md", ["docs/**"]),
        ]
        (root / "docs/DOCS_MANIFEST.json").write_text(json.dumps({"version": 1, "documents": documents}), encoding="utf-8")
        (root / "docs/DOCS_REGISTRY.md").write_text(health.registry_text(documents), encoding="utf-8")
        return root

    @staticmethod
    def entry(path: str, globs: list[str]) -> dict:
        return {"path": path, "title": path, "class": "rule", "owner": "test", "purpose": "test", "review_days": 30, "last_verified": "2026-07-17", "source_globs": globs, "impact": "required"}

    def test_complete_manifest_passes(self) -> None:
        root = self.build_root()
        self.assertEqual([], health.validate(root, full_links=True))

    def test_conflict_marker_fails(self) -> None:
        root = self.build_root()
        (root / "AGENTS.md").write_text("<<<<<<< HEAD\n", encoding="utf-8")
        findings = health.validate(root)
        self.assertTrue(any("merge-conflict marker" in item.message for item in findings), findings)

    def test_inline_conflict_marker_explanation_is_allowed(self) -> None:
        root = self.build_root()
        (root / "AGENTS.md").write_text("Reject `<<<<<<<`, `=======`, `>>>>>>>` markers.\n", encoding="utf-8")
        self.assertEqual([], health.validate(root))

    def test_registry_drift_fails(self) -> None:
        root = self.build_root()
        (root / "docs/DOCS_REGISTRY.md").write_text("stale\n", encoding="utf-8")
        findings = health.validate(root)
        self.assertTrue(any("registry does not match" in item.message for item in findings), findings)

    def test_unregistered_index_link_fails(self) -> None:
        root = self.build_root()
        (root / "docs/OTHER.md").write_text("# Other\n", encoding="utf-8")
        with (root / "docs/DOCS_INDEX.md").open("a", encoding="utf-8") as handle:
            handle.write("[Other](OTHER.md)\n")
        findings = health.validate(root)
        self.assertTrue(any("unregistered document" in item.message for item in findings), findings)

    def test_context_routing_orders_required_first(self) -> None:
        root = self.build_root()
        matches = health.context_documents(["src/MathLearning.Api/Program.cs"], root)
        self.assertEqual("AGENTS.md", matches[0][0]["path"])

    def test_broken_registered_link_fails(self) -> None:
        root = self.build_root()
        (root / "AGENTS.md").write_text("[Missing](missing.md)\n", encoding="utf-8")
        findings = health.validate(root, full_links=True)
        self.assertTrue(any("broken local link" in item.message for item in findings), findings)


if __name__ == "__main__":
    unittest.main()
