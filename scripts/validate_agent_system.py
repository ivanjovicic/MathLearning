#!/usr/bin/env python3
"""Validate backend AI-agent documentation wiring, speed gates and CI routing."""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED_PATHS = (
    "AGENTS.md",
    "docs/DOCS_INDEX.md",
    ".ai/README.md",
    ".ai/SOURCE_OF_TRUTH.md",
    ".ai/TOKEN_BUDGETS.md",
    ".ai/VALIDATION_SELECTOR.md",
    ".ai/PROMPT_LINT_CHECKLIST.md",
    ".ai/RUN_LOG_TEMPLATE.md",
    ".ai/runs/README.md",
    "docs/AGENT_COMMAND_PLAYBOOK.md",
    "docs/AGENT_RUN_LOG_ENFORCEMENT.md",
    "docs/prompt_queues/README.md",
    "docs/prompt_queues/PROMPT_LIFECYCLE.md",
    "docs/ai/TASK_TEMPLATE.md",
    "docs/ai/learning/MISTAKE_LEDGER.md",
    "docs/ai/learning/MISTAKE_INDEX.json",
    "scripts/run_guarded.py",
    "scripts/agent_run.py",
    "scripts/analyze_agent_runs.py",
    "scripts/validate_agent_prompt.py",
    "scripts/validate_agent_evidence.py",
    "scripts/validate_agent_system.py",
    "scripts/ci/classify_backend_changes.py",
    ".github/workflows/agent-system-validation.yml",
    ".github/workflows/database-validation.yml",
)
DOCS_TO_CHECK = (
    "AGENTS.md",
    "docs/DOCS_INDEX.md",
    ".ai/README.md",
    ".ai/SOURCE_OF_TRUTH.md",
    ".ai/TOKEN_BUDGETS.md",
    ".ai/VALIDATION_SELECTOR.md",
    ".ai/PROMPT_LINT_CHECKLIST.md",
    ".ai/RUN_LOG_TEMPLATE.md",
    ".ai/runs/README.md",
    "docs/AGENT_COMMAND_PLAYBOOK.md",
    "docs/AGENT_RUN_LOG_ENFORCEMENT.md",
    "docs/prompt_queues/PROMPT_LIFECYCLE.md",
    "docs/ai/TASK_TEMPLATE.md",
)
REQUIRED_REFERENCES = {
    "AGENTS.md": (
        ".ai/README.md", "MISTAKE_INDEX.json", "agent_run.py", "validate_agent_evidence.py --changed-from"
    ),
    "docs/DOCS_INDEX.md": (
        "MISTAKE_INDEX.json", "agent_run.py", "analyze_agent_runs.py", "classify_backend_changes.py"
    ),
    ".ai/README.md": (
        "scripts/agent_run.py", "MISTAKE_INDEX.json", "validate_agent_evidence.py --changed-from"
    ),
    ".ai/SOURCE_OF_TRUTH.md": (
        "MISTAKE_INDEX.json", "scripts/agent_run.py"
    ),
    ".github/workflows/agent-system-validation.yml": (
        "scripts/test_agent_run.py", "scripts/test_validate_agent_evidence.py",
        "scripts/test_analyze_agent_runs.py", "scripts/ci/test_classify_backend_changes.py"
    ),
    ".github/workflows/database-validation.yml": (
        "classify_backend_changes.py", "database-suite", "validate-database"
    ),
}
FORBIDDEN_RUNTIME_COMMANDS = ("flutter analyze", "flutter test", "dart format", "dart analyze")
FORBIDDEN_SLOW_RULES = (
    "read this ledger", "read the whole mistake ledger", "copy the full run log template by hand"
)
LINK_RE = re.compile(r"\[[^\]]+\]\(([^)]+)\)")


@dataclass(frozen=True)
class Finding:
    severity: str
    path: Path
    message: str

    def render(self, root: Path) -> str:
        try:
            shown = self.path.relative_to(root)
        except ValueError:
            shown = self.path
        return f"[{self.severity}] {shown} - {self.message}"


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def resolve_link(source: Path, target: str, root: Path) -> Path | None:
    cleaned = target.strip().strip("<>")
    if not cleaned or cleaned.startswith(("http://", "https://", "mailto:", "#")):
        return None
    cleaned = cleaned.split("#", 1)[0]
    if not cleaned or "<" in cleaned or ">" in cleaned:
        return None
    if cleaned.startswith("/"):
        return root / cleaned.lstrip("/")
    return (source.parent / cleaned).resolve()


def validate(root: Path = ROOT) -> list[Finding]:
    findings: list[Finding] = []
    for relative in REQUIRED_PATHS:
        path = root / relative
        if not path.exists():
            findings.append(Finding("FAIL", path, "required agent-speed file is missing"))
    for relative, references in REQUIRED_REFERENCES.items():
        path = root / relative
        if not path.exists():
            continue
        text = read_text(path)
        for reference in references:
            if reference not in text:
                findings.append(Finding("FAIL", path, f"missing required reference: {reference}"))
    for relative in DOCS_TO_CHECK:
        path = root / relative
        if not path.exists():
            continue
        text = read_text(path)
        lowered = text.casefold()
        for command in FORBIDDEN_RUNTIME_COMMANDS:
            if command in lowered:
                findings.append(Finding("FAIL", path, f"Flutter-only command leaked into backend docs: {command}"))
        if relative in {"AGENTS.md", ".ai/README.md", "docs/AGENT_RUN_LOG_ENFORCEMENT.md"}:
            for phrase in FORBIDDEN_SLOW_RULES:
                if phrase in lowered:
                    findings.append(Finding("FAIL", path, f"slow default rule remains: {phrase}"))
        for raw_target in LINK_RE.findall(text):
            target = resolve_link(path, raw_target, root)
            if target is not None and not target.exists():
                findings.append(Finding("FAIL", path, f"broken relative link: {raw_target}"))
    index = root / "docs/ai/learning/MISTAKE_INDEX.json"
    if index.exists():
        try:
            data = json.loads(read_text(index))
            if data.get("version") != 1 or not data.get("areas"):
                findings.append(Finding("FAIL", index, "mistake index must have version 1 and areas"))
            ledger = read_text(root / "docs/ai/learning/MISTAKE_LEDGER.md") if (root / "docs/ai/learning/MISTAKE_LEDGER.md").exists() else ""
            ids = set(re.findall(r"BACKEND-MISTAKE-[A-Z0-9-]+", json.dumps(data)))
            for mistake_id in sorted(ids):
                if mistake_id not in ledger:
                    findings.append(Finding("FAIL", index, f"mistake index references unknown ID: {mistake_id}"))
        except json.JSONDecodeError as exc:
            findings.append(Finding("FAIL", index, f"invalid JSON: {exc}"))
    agents = root / "AGENTS.md"
    if agents.exists() and "MathLearning Backend" not in read_text(agents):
        findings.append(Finding("FAIL", agents, "backend rulebook title/identity is missing"))
    return findings


def main() -> int:
    findings = validate(ROOT)
    failures = [item for item in findings if item.severity == "FAIL"]
    print(f"Backend agent-system validation: failures={len(failures)}")
    for finding in findings:
        print(finding.render(ROOT))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
