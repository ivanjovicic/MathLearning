#!/usr/bin/env python3
"""Validate backend AI-agent documentation wiring and mechanical gates."""
from __future__ import annotations

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
    "docs/AGENT_COMMAND_PLAYBOOK.md",
    "docs/prompt_queues/README.md",
    "docs/prompt_queues/PROMPT_LIFECYCLE.md",
    "docs/ai/TASK_TEMPLATE.md",
    "scripts/run_guarded.py",
    "scripts/test_run_guarded.py",
    "scripts/validate_agent_prompt.py",
    "scripts/test_validate_agent_prompt.py",
    "scripts/validate_agent_evidence.py",
    "scripts/validate_agent_system.py",
    "scripts/test_validate_agent_system.py",
    ".github/workflows/agent-system-validation.yml",
)
DOCS_TO_CHECK = (
    "AGENTS.md",
    "docs/DOCS_INDEX.md",
    ".ai/README.md",
    ".ai/SOURCE_OF_TRUTH.md",
    ".ai/TOKEN_BUDGETS.md",
    ".ai/VALIDATION_SELECTOR.md",
    ".ai/PROMPT_LINT_CHECKLIST.md",
    "docs/AGENT_COMMAND_PLAYBOOK.md",
    "docs/prompt_queues/README.md",
    "docs/prompt_queues/PROMPT_LIFECYCLE.md",
    "docs/ai/TASK_TEMPLATE.md",
)
REQUIRED_REFERENCES = {
    "AGENTS.md": (
        ".ai/README.md",
        ".ai/VALIDATION_SELECTOR.md",
        ".ai/PROMPT_LINT_CHECKLIST.md",
        "AGENT_COMMAND_PLAYBOOK.md",
        "prompt_queues/README.md",
    ),
    "docs/DOCS_INDEX.md": (
        ".ai/README.md",
        ".ai/SOURCE_OF_TRUTH.md",
        ".ai/TOKEN_BUDGETS.md",
        ".ai/VALIDATION_SELECTOR.md",
        "AGENT_COMMAND_PLAYBOOK.md",
        "prompt_queues/README.md",
    ),
    ".github/workflows/agent-system-validation.yml": (
        "scripts/test_run_guarded.py",
        "scripts/test_validate_agent_prompt.py",
        "scripts/validate_agent_system.py",
    ),
}
FORBIDDEN_RUNTIME_COMMANDS = (
    "flutter analyze",
    "flutter test",
    "dart format",
    "dart analyze",
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
            findings.append(Finding("FAIL", path, "required agent-system file is missing"))

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
                findings.append(Finding("FAIL", path, f"Flutter-only runtime command leaked into backend docs: {command}"))
        for raw_target in LINK_RE.findall(text):
            target = resolve_link(path, raw_target, root)
            if target is not None and not target.exists():
                findings.append(Finding("FAIL", path, f"broken relative link: {raw_target}"))

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
