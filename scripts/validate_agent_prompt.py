#!/usr/bin/env python3
"""Validate forward-only backend agent prompt contracts.

Historical queue prose is not migrated automatically. A file or prompt section is
validated when it declares ``Prompt contract: v2`` or when ``--strict`` is used.
"""
from __future__ import annotations

import argparse
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = "ivanjovicic/MathLearning"
PROMPT_HEADING_RE = re.compile(r"^#{1,4}\s+([A-Z][A-Z0-9-]{2,})\s+[—-]\s+.+$", re.MULTILINE)
CONTRACT_RE = re.compile(r"^\s*Prompt contract\s*:\s*v2\s*$", re.IGNORECASE | re.MULTILINE)
ADMISSION_RE = re.compile(r"^\s*Prompt admission\s*:\s*v3\s*$", re.IGNORECASE | re.MULTILINE)
FIELD_RE_TEMPLATE = r"^\s*{name}\s*:\s*(.*)$"
LANES = {"known-fix", "investigation", "docs-evidence", "audit", "review", "validation-only", "implementation", "tests"}
BUDGETS = {"low", "medium", "high"}
TEMPLATE_PATHS = {"docs/ai/TASK_TEMPLATE.md"}
FORBIDDEN_BACKEND_TOKENS = (
    "flutter analyze",
    "flutter test",
    "dart format",
    "dart analyze",
    "lib/",
    "test/widget",
)
CHAIN_RE = re.compile(r"(?:&&|\|\||;)")

REQUIRED_FIELDS: dict[str, tuple[str, ...]] = {
    "repository": ("Repository", "Use only this repository"),
    "prompt id": ("Prompt ID",),
    "queue": ("Queue",),
    "run lane": ("Run lane", "Run mode"),
    "token budget": ("Token budget", "Budget"),
    "timebox": ("Timebox", "Time budget"),
    "task": ("Task",),
    "source of truth": ("Source of truth",),
    "interpretation": ("Interpretation before work", "Interpretation contract"),
    "ambiguity rule": ("Ambiguity rule",),
    "risk/ownership model": ("Risk/ownership model", "Risk and ownership model"),
    "failure-mode matrix": ("Failure-mode matrix", "Failure mode matrix", "Adversarial cases"),
    "execution packet": ("Execution packet", "Bounded execution packet"),
    "owned paths": ("Owned paths",),
    "avoid paths": ("Avoid paths", "Non-goals"),
    "documentation impact": ("Documentation impact",),
    "acceptance criteria": ("Acceptance criteria", "Acceptance"),
    "proof required": ("Proof required", "Execution proof"),
    "validation": ("Validation",),
    "completion gate": ("Completion gate", "Closure gate"),
    "stop conditions": ("Stop conditions", "Stop rules", "Stop if"),
    "evidence": ("Evidence", "Run log", "Evidence path"),
}
ADMISSION_FIELDS: dict[str, tuple[str, ...]] = {
    "problem evidence": ("Problem evidence",),
    "deduplication check": ("Deduplication check",),
    "priority rationale": ("Priority rationale",),
    "dependencies/collisions": ("Dependencies/collisions",),
    "owner boundary": ("Owner boundary",),
    "queue placement": ("Queue placement",),
}
ALL_ALIASES = tuple(alias for group in (*REQUIRED_FIELDS.values(), *ADMISSION_FIELDS.values()) for alias in group)
NEXT_FIELD_RE = re.compile(
    rf"^\s*(?:{'|'.join(re.escape(name) for name in sorted(set(ALL_ALIASES), key=len, reverse=True))})\s*:",
    re.IGNORECASE,
)


@dataclass(frozen=True)
class Finding:
    severity: str
    path: Path
    prompt_id: str
    message: str

    def render(self) -> str:
        try:
            shown = self.path.relative_to(ROOT)
        except ValueError:
            shown = self.path
        return f"[{self.severity}] {shown} [{self.prompt_id}] - {self.message}"


@dataclass(frozen=True)
class PromptSection:
    prompt_id: str
    text: str


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def field_value(text: str, aliases: Iterable[str]) -> str | None:
    for alias in aliases:
        match = re.search(FIELD_RE_TEMPLATE.format(name=re.escape(alias)), text, re.IGNORECASE | re.MULTILINE)
        if match:
            return match.group(1).strip()
    return None


def field_block(text: str, aliases: Iterable[str]) -> str | None:
    for alias in aliases:
        match = re.search(FIELD_RE_TEMPLATE.format(name=re.escape(alias)), text, re.IGNORECASE | re.MULTILINE)
        if not match:
            continue
        lines = [match.group(1).strip()] if match.group(1).strip() else []
        for line in text[match.end():].splitlines():
            if NEXT_FIELD_RE.match(line) or re.match(r"^#{1,6}\s+", line):
                break
            lines.append(line.rstrip())
        return "\n".join(lines).strip()
    return None


def list_item_count(raw: str | None) -> int:
    return sum(1 for line in (raw or "").splitlines() if re.match(r"^\s*(?:[-*]|\d+\.)\s+\S", line))


def parse_minutes(raw: str | None) -> int | None:
    if not raw:
        return None
    match = re.search(r"(\d{1,3})\s*(?:[- ]?min(?:ute)?s?)", raw, re.IGNORECASE)
    return int(match.group(1)) if match else None


def prompt_sections(text: str) -> list[PromptSection]:
    matches = list(PROMPT_HEADING_RE.finditer(text))
    if not matches:
        prompt_id = field_value(text, ("Prompt ID",)) or "FILE"
        return [PromptSection(prompt_id, text)]
    result: list[PromptSection] = []
    for index, match in enumerate(matches):
        end = matches[index + 1].start() if index + 1 < len(matches) else len(text)
        result.append(PromptSection(match.group(1), text[match.start():end]))
    return result


def command_lines(text: str) -> list[str]:
    result: list[str] = []
    in_fence = False
    for raw in text.splitlines():
        line = raw.strip()
        if line.startswith("```"):
            in_fence = not in_fence
            continue
        if not in_fence or not line or line.startswith("#"):
            continue
        if any(line.startswith(prefix) for prefix in ("dotnet ", "python ", "git ", "gh ", "powershell ")):
            result.append(line)
    return result


def validate_section(path: Path, section: PromptSection, *, strict: bool, template: bool = False) -> list[Finding]:
    text = section.text
    if not strict and not CONTRACT_RE.search(text):
        return []

    findings: list[Finding] = []
    prompt_id = section.prompt_id
    if not CONTRACT_RE.search(text):
        findings.append(Finding("FAIL", path, prompt_id, "missing Prompt contract: v2"))

    for label, aliases in REQUIRED_FIELDS.items():
        value = field_block(text, aliases)
        if value is None or not value.strip():
            findings.append(Finding("FAIL", path, prompt_id, f"missing required field: {aliases[0]}:"))

    repository = field_value(text, REQUIRED_FIELDS["repository"])
    if repository and REPOSITORY.casefold() not in repository.casefold():
        findings.append(Finding("FAIL", path, prompt_id, f"Repository must target {REPOSITORY}"))

    declared_id = field_value(text, REQUIRED_FIELDS["prompt id"])
    if declared_id and prompt_id != "FILE" and declared_id.strip("`") != prompt_id:
        findings.append(Finding("FAIL", path, prompt_id, f"Prompt ID field does not match heading: {declared_id}"))

    lane = (field_value(text, REQUIRED_FIELDS["run lane"]) or "").casefold()
    if lane and not template and lane not in LANES:
        findings.append(Finding("FAIL", path, prompt_id, f"unsupported run lane: {lane}"))

    budget = (field_value(text, REQUIRED_FIELDS["token budget"]) or "").casefold()
    if budget and not template and budget not in BUDGETS:
        findings.append(Finding("FAIL", path, prompt_id, f"token budget must be low, medium or high: {budget}"))

    minutes = parse_minutes(field_value(text, REQUIRED_FIELDS["timebox"]))
    if minutes is None:
        findings.append(Finding("FAIL", path, prompt_id, "Timebox must contain an explicit minute value"))
    elif minutes > 30:
        findings.append(Finding("FAIL", path, prompt_id, "Timebox must not exceed 30 minutes"))

    failure_modes = field_block(text, REQUIRED_FIELDS["failure-mode matrix"])
    if failure_modes is not None and list_item_count(failure_modes) < 2:
        findings.append(Finding("FAIL", path, prompt_id, "Failure-mode matrix needs at least two concrete cases"))

    acceptance = field_block(text, REQUIRED_FIELDS["acceptance criteria"])
    if acceptance is not None and list_item_count(acceptance) < 3:
        findings.append(Finding("FAIL", path, prompt_id, "Acceptance criteria need at least three observable items"))

    packet = field_block(text, REQUIRED_FIELDS["execution packet"])
    if packet is not None and list_item_count(packet) < 5:
        findings.append(Finding("FAIL", path, prompt_id, "Execution packet needs at least five bounded list items"))

    lowered = text.casefold()
    for token in FORBIDDEN_BACKEND_TOKENS:
        if token in lowered:
            findings.append(Finding("FAIL", path, prompt_id, f"Flutter-specific token is not valid in a backend prompt: {token}"))

    for command in command_lines(text):
        if CHAIN_RE.search(command):
            findings.append(Finding("FAIL", path, prompt_id, f"command chaining is forbidden: {command}"))
        if len(command) > 180:
            findings.append(Finding("FAIL", path, prompt_id, "command exceeds 180 characters"))
        if command.startswith(("dotnet ", "git push", "git fetch", "gh ")) and "scripts/run_guarded.py" not in command:
            findings.append(Finding("FAIL", path, prompt_id, f"potentially blocking command must use scripts/run_guarded.py: {command}"))

    if ADMISSION_RE.search(text):
        for label, aliases in ADMISSION_FIELDS.items():
            value = field_block(text, aliases)
            if value is None or not value.strip():
                findings.append(Finding("FAIL", path, prompt_id, f"missing admission field: {aliases[0]}:"))
        for label in ("problem evidence", "deduplication check", "dependencies/collisions"):
            block = field_block(text, ADMISSION_FIELDS[label])
            minimum = 2 if label != "deduplication check" else 3
            if block is not None and list_item_count(block) < minimum:
                findings.append(Finding("FAIL", path, prompt_id, f"{ADMISSION_FIELDS[label][0]} needs at least {minimum} list items"))

    return findings


def changed_markdown_files(base: str) -> list[Path]:
    completed = subprocess.run(
        ["git", "diff", "--diff-filter=AM", "--name-only", f"{base}...HEAD"],
        cwd=ROOT,
        text=True,
        capture_output=True,
        check=True,
        timeout=30,
    )
    result: list[Path] = []
    for raw in completed.stdout.splitlines():
        rel = raw.strip().replace("\\", "/")
        if not rel.endswith(".md"):
            continue
        if rel.startswith("docs/prompt_queues/") or rel.startswith("docs/ai/"):
            result.append(ROOT / rel)
    return result


def default_files() -> list[Path]:
    result: set[Path] = set()
    for root in (ROOT / "docs" / "prompt_queues", ROOT / "docs" / "ai"):
        if root.exists():
            result.update(root.rglob("*.md"))
    return sorted(result)


def validate_files(paths: Iterable[Path], *, strict: bool = False) -> list[Finding]:
    findings: list[Finding] = []
    for path in paths:
        if not path.exists():
            findings.append(Finding("FAIL", path, "FILE", "file does not exist"))
            continue
        text = read_text(path)
        sections = prompt_sections(text)
        if strict and not any(CONTRACT_RE.search(section.text) for section in sections):
            findings.append(Finding("FAIL", path, "FILE", "strict mode requires Prompt contract: v2"))
            continue
        template = path.relative_to(ROOT).as_posix() in TEMPLATE_PATHS if path.is_relative_to(ROOT) else False
        for section in sections:
            findings.extend(validate_section(path, section, strict=strict, template=template))
    return findings


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("files", nargs="*")
    parser.add_argument("--strict", action="store_true")
    parser.add_argument("--changed-from")
    args = parser.parse_args(argv)

    if args.changed_from:
        paths = changed_markdown_files(args.changed_from)
    elif args.files:
        paths = [Path(item).resolve() if Path(item).is_absolute() else ROOT / item for item in args.files]
    else:
        paths = default_files()

    findings = validate_files(paths, strict=args.strict)
    failures = [item for item in findings if item.severity == "FAIL"]
    print(f"Backend prompt validation: files={len(paths)} failures={len(failures)}")
    for finding in findings:
        print(finding.render())
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
