#!/usr/bin/env python3
"""Validate backend queue evidence and compact/legacy run logs.

Fast path:
    python scripts/validate_agent_evidence.py --changed-from <base> --verify-git

The changed-range mode validates only changed queue rows and changed/referenced run
logs, so historical evidence debt cannot hide or block the current task.
"""
from __future__ import annotations

import argparse
import re
import subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

ROOT = Path(__file__).resolve().parents[1]
QUEUE_DIR = ROOT / "docs" / "prompt_queues"
RUNS_DIR = ROOT / ".ai" / "runs"
MISTAKE_LEDGER = ROOT / "docs" / "ai" / "learning" / "MISTAKE_LEDGER.md"
STRICT_GATE_DATE = "2026-07-01"

DONE_RE = re.compile(r"\bDone\b(?:\s+(\d{1,3})%)?", re.IGNORECASE)
DATE_RE = re.compile(r"20\d{2}-\d{2}-\d{2}")
RUN_PATH_RE = re.compile(r"\.ai/runs/[A-Za-z0-9_./\-]+\.md")
MISTAKE_ID_RE = re.compile(r"\bBACKEND-MISTAKE-[A-Z0-9]+(?:-[A-Z0-9]+)*\b")
COMMIT_RE = re.compile(r"\b[0-9a-f]{7,40}\b", re.IGNORECASE)
HUNK_RE = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")
ALLOWED_LANES = {
    "known-fix", "investigation", "validation-only", "tests", "docs-evidence", "audit", "review"
}
ALLOWED_STATES = {
    "In progress", "Needs validation", "Needs evidence sync", "Needs merge", "Blocked", "Done", "Archived"
}
BUDGET_LIMITS = {
    "micro": {"inspected": 6, "changed": 2, "searches": 1},
    "low": {"inspected": 11, "changed": 3, "searches": 2},
    "medium": {"inspected": 20, "changed": 6, "searches": 4},
    "high": {"inspected": 28, "changed": 10, "searches": 6},
}
MISSING_PROOF_WORDS = (
    "missing", "not run", "not verified", "not implemented", "not created", "evidence gap", "without proof"
)
PLACEHOLDER_COMMIT_WORDS = (
    "uncommitted", "pending", "unknown commit", "unknown-commit", "unknown-not-recorded", "open"
)


@dataclass(frozen=True)
class Finding:
    severity: str
    path: Path
    line_no: int
    message: str

    def format(self, root: Path = ROOT) -> str:
        try:
            shown = self.path.relative_to(root)
        except ValueError:
            shown = self.path
        return f"[{self.severity}] {shown}:{self.line_no} - {self.message}"


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def field_value(text: str, name: str) -> str | None:
    match = re.search(rf"^\s*{re.escape(name)}\s*:\s*(.*)$", text, re.IGNORECASE | re.MULTILINE)
    return match.group(1).strip() if match else None


def section_text(text: str, heading: str) -> str:
    match = re.search(rf"^##\s+{re.escape(heading)}\s*$", text, re.IGNORECASE | re.MULTILINE)
    if not match:
        return ""
    tail = text[match.end():]
    boundary = re.search(r"^##\s+", tail, re.MULTILINE)
    return tail[: boundary.start()] if boundary else tail


def load_mistake_ids(root: Path = ROOT) -> set[str]:
    path = root / MISTAKE_LEDGER.relative_to(ROOT)
    return set(MISTAKE_ID_RE.findall(read_text(path))) if path.exists() else set()


def iter_queue_files(root: Path = ROOT) -> Iterable[Path]:
    directory = root / QUEUE_DIR.relative_to(ROOT)
    return sorted(directory.glob("*.md")) if directory.exists() else []


def iter_run_logs(root: Path = ROOT) -> Iterable[Path]:
    directory = root / RUNS_DIR.relative_to(ROOT)
    return sorted(directory.glob("*.md")) if directory.exists() else []


def is_table_row(line: str) -> bool:
    stripped = line.strip()
    return stripped.startswith("|") and stripped.endswith("|") and "|---" not in stripped


def first_cell(line: str) -> str:
    cells = [part.strip() for part in line.strip().strip("|").split("|")]
    return cells[0] if cells else "unknown"


def is_modern_row(line: str) -> bool:
    date_match = DATE_RE.search(line)
    if date_match and date_match.group(0) >= STRICT_GATE_DATE:
        return True
    lowered = line.casefold()
    return "run log:" in lowered or "evidence gap" in lowered


def extract_run_log_paths(line: str) -> list[str]:
    return RUN_PATH_RE.findall(line)


def row_score(line: str) -> int | None:
    match = DONE_RE.search(line)
    return int(match.group(1)) if match and match.group(1) else None


def validate_queue_row(path: Path, line_no: int, line: str, root: Path = ROOT) -> tuple[list[Finding], set[Path]]:
    findings: list[Finding] = []
    referenced: set[Path] = set()
    if not is_table_row(line) or not DONE_RE.search(line):
        return findings, referenced
    severity = "FAIL" if is_modern_row(line) else "WARN"
    prompt_id = first_cell(line)
    required = ("Run log:", "Validation:", "Residual risk:")
    missing = [field for field in required if field.casefold() not in line.casefold()]
    if missing:
        findings.append(Finding(severity, path, line_no, f"{prompt_id} Done row missing compact evidence fields: {', '.join(missing)}"))
    paths = extract_run_log_paths(line)
    if "run log:" in line.casefold() and "run log: fallback" not in line.casefold() and not paths:
        findings.append(Finding(severity, path, line_no, f"{prompt_id} Run log field has no .ai/runs/*.md path"))
    for raw in paths:
        target = root / raw
        referenced.add(target)
        if not target.exists():
            findings.append(Finding("FAIL", path, line_no, f"{prompt_id} references missing run log: {raw}"))
    score = row_score(line)
    if score == 100:
        residual = line[line.casefold().find("residual risk:"):].casefold()
        if any(word in residual for word in MISSING_PROOF_WORDS):
            findings.append(Finding("FAIL", path, line_no, f"{prompt_id} claims 100% while residual proof is missing"))
    if any(word in line.casefold() for word in PLACEHOLDER_COMMIT_WORDS) and "commit" in line.casefold():
        if "commit self" not in line.casefold() and "commit: self" not in line.casefold():
            findings.append(Finding(severity, path, line_no, f"{prompt_id} uses unresolved commit placeholder"))
    return findings, referenced


def parse_int_field(text: str, name: str) -> int | None:
    raw = field_value(text, name)
    return int(raw) if raw and raw.isdigit() else None


def commit_value(text: str) -> str:
    return field_value(text, "Commit SHA") or section_text(text, "Commit SHA").strip(" \n-")


def resolve_self_commit(path: Path, root: Path) -> str | None:
    try:
        result = subprocess.run(
            ["git", "log", "-1", "--format=%H", "--", str(path.relative_to(root))],
            cwd=root, text=True, capture_output=True, check=True, timeout=15,
        )
    except (OSError, subprocess.SubprocessError, ValueError):
        return None
    value = result.stdout.strip()
    return value if COMMIT_RE.fullmatch(value) else None


def validate_commit(path: Path, text: str, strict: bool, verify_git: bool, root: Path) -> list[Finding]:
    findings: list[Finding] = []
    value = commit_value(text).strip()
    severity = "FAIL" if strict else "WARN"
    if not value:
        return [Finding(severity, path, 1, "run log missing Commit SHA")]
    lowered = value.casefold()
    if lowered == "self":
        if verify_git and not resolve_self_commit(path, root):
            findings.append(Finding("FAIL", path, 1, "Commit SHA: self could not be resolved from git history"))
        return findings
    if any(word in lowered for word in PLACEHOLDER_COMMIT_WORDS):
        findings.append(Finding(severity, path, 1, "Commit SHA must be a real hash or the self sentinel"))
    elif not COMMIT_RE.search(value):
        findings.append(Finding(severity, path, 1, "Commit SHA must contain a real hash or self"))
    return findings


def completion_score(text: str) -> int | None:
    direct = field_value(text, "Completion %")
    if direct and direct.isdigit():
        return int(direct)
    match = re.search(r"Completion %\s*\n\s*[-*]?\s*(\d{1,3})", text, re.IGNORECASE)
    return int(match.group(1)) if match else None


def validate_v2_log(path: Path, text: str, known_mistakes: set[str], verify_git: bool, root: Path) -> list[Finding]:
    findings: list[Finding] = []
    required = (
        "Prompt ID", "Queue", "Agent/tool", "Model provider", "Model name/id", "Client/IDE",
        "Run mode", "Token budget", "Started at UTC", "Completed at UTC", "Elapsed time",
        "Relevant prior mistakes read", "How this run avoids prior mistakes", "Owner/hypothesis",
        "Files inspected", "Files changed", "Searches", "Validation runs", "Failed retries",
        "Mistakes observed", "Waste", "Missed", "Follow-up", "Residual risk",
        "Documentation impact", "Cross-repo impact", "State", "Branch/PR", "Commit SHA", "Completion %",
    )
    for name in required:
        value = field_value(text, name)
        if value is None or not value:
            findings.append(Finding("FAIL", path, 1, f"v2 run log missing field: {name}"))
        elif value.casefold() == "open":
            findings.append(Finding("FAIL", path, 1, f"v2 run log still open: {name}"))
    lane = field_value(text, "Run mode") or ""
    if lane not in ALLOWED_LANES:
        findings.append(Finding("FAIL", path, 1, f"Run mode must be one lane, got: {lane}"))
    budget = (field_value(text, "Token budget") or "").casefold()
    if budget not in BUDGET_LIMITS:
        findings.append(Finding("FAIL", path, 1, f"unknown Token budget: {budget}"))
    state = field_value(text, "State") or ""
    if state not in ALLOWED_STATES:
        findings.append(Finding("FAIL", path, 1, f"unknown delivery State: {state}"))
    for name in ("Files inspected", "Files changed", "Searches", "Validation runs", "Failed retries"):
        if parse_int_field(text, name) is None:
            findings.append(Finding("FAIL", path, 1, f"{name} must be numeric"))
    score = completion_score(text)
    if score is None or not 0 <= score <= 100:
        findings.append(Finding("FAIL", path, 1, "Completion % must be 0..100"))
        score = 0
    if budget in BUDGET_LIMITS:
        limits = BUDGET_LIMITS[budget]
        values = {
            "inspected": parse_int_field(text, "Files inspected") or 0,
            "changed": parse_int_field(text, "Files changed") or 0,
            "searches": parse_int_field(text, "Searches") or 0,
        }
        breaches = [f"{name}={values[name]}>{limits[name]}" for name in values if values[name] > limits[name]]
        if breaches and score > 79:
            findings.append(Finding("FAIL", path, 1, "budget breach requires Completion <=79: " + ", ".join(breaches)))
    mistakes = field_value(text, "Mistakes observed") or ""
    if mistakes.casefold() != "none":
        ids = set(MISTAKE_ID_RE.findall(mistakes))
        if not ids:
            findings.append(Finding("FAIL", path, 1, "Mistakes observed must be none or include BACKEND-MISTAKE-* IDs"))
        for unknown in sorted(ids - known_mistakes):
            findings.append(Finding("FAIL", path, 1, f"unknown mistake ID: {unknown}"))
        if "repeated" in mistakes.casefold() and "prevention=" not in mistakes.casefold():
            findings.append(Finding("FAIL", path, 1, "repeated mistake must include prevention=<change>"))
    validation = (field_value(text, "Validation run") or "").casefold()
    failed_validation = any(word in validation for word in (" fail", "failed", "failure", "timeout"))
    if failed_validation and (state == "Done" or score > 79):
        findings.append(Finding("FAIL", path, 1, "failed validation requires a non-Done state and Completion <=79"))
    residual = (field_value(text, "Residual risk") or "").casefold()
    if score == 100 and residual not in {"none", "none.", "no material residual risk"}:
        findings.append(Finding("FAIL", path, 1, "100% completion requires no material residual risk"))
    if state == "Done" and score < 95:
        findings.append(Finding("FAIL", path, 1, "Done state requires Completion >=95"))
    findings.extend(validate_commit(path, text, True, verify_git, root))
    if len(text.splitlines()) > 90:
        findings.append(Finding("WARN", path, 1, "v2 run log exceeds 90 lines; keep evidence compact"))
    return findings


def validate_legacy_log(path: Path, text: str, known_mistakes: set[str], strict: bool, verify_git: bool, root: Path) -> list[Finding]:
    severity = "FAIL" if strict else "WARN"
    findings: list[Finding] = []
    required = (
        "Prompt ID", "Queue", "Agent/tool", "Model provider", "Model name/id", "Client/IDE",
        "Run mode", "Token budget", "Elapsed time", "Phase time breakdown",
    )
    for name in required:
        if field_value(text, name) is None:
            findings.append(Finding(severity, path, 1, f"legacy run log missing field: {name}"))
    mistakes_section = section_text(text, "Mistakes observed")
    if not mistakes_section and field_value(text, "Mistakes observed") is None:
        findings.append(Finding(severity, path, 1, "legacy run log missing Mistakes observed"))
    else:
        content = mistakes_section or field_value(text, "Mistakes observed") or ""
        if "none" not in content.casefold():
            ids = set(MISTAKE_ID_RE.findall(content))
            if not ids:
                findings.append(Finding(severity, path, 1, "legacy mistakes are not none and contain no ID"))
            for unknown in sorted(ids - known_mistakes):
                findings.append(Finding("FAIL", path, 1, f"unknown mistake ID: {unknown}"))
    score = completion_score(text)
    residual = section_text(text, "Residual risk").casefold()
    if score == 100 and any(word in residual for word in MISSING_PROOF_WORDS):
        findings.append(Finding("FAIL", path, 1, "legacy log claims 100% with missing proof"))
    findings.extend(validate_commit(path, text, strict, verify_git, root))
    return findings


def validate_run_log(path: Path, known_mistakes: set[str], referenced: set[Path], *, verify_git: bool = False, root: Path = ROOT) -> list[Finding]:
    text = read_text(path)
    if (field_value(text, "Evidence format") or "").casefold() == "v2":
        return validate_v2_log(path, text, known_mistakes, verify_git, root)
    strict = path in referenced or any(marker in text for marker in ("Relevant prior mistakes read:", "## Mistakes observed"))
    return validate_legacy_log(path, text, known_mistakes, strict, verify_git, root)


def git_changed_paths(base: str, root: Path = ROOT) -> list[str]:
    result = subprocess.run(
        ["git", "diff", "--diff-filter=AM", "--name-only", f"{base}...HEAD"],
        cwd=root, text=True, capture_output=True, check=True, timeout=30,
    )
    return [line.strip().replace("\\", "/") for line in result.stdout.splitlines() if line.strip()]


def changed_line_numbers(path: Path, base: str, root: Path = ROOT) -> set[int]:
    try:
        rel = path.relative_to(root).as_posix()
    except ValueError:
        return set()
    result = subprocess.run(
        ["git", "diff", "--unified=0", f"{base}...HEAD", "--", rel],
        cwd=root, text=True, capture_output=True, check=True, timeout=30,
    )
    numbers: set[int] = set()
    for line in result.stdout.splitlines():
        match = HUNK_RE.match(line)
        if not match:
            continue
        start = int(match.group(1))
        count = int(match.group(2) or "1")
        numbers.update(range(start, start + count))
    return numbers


def validate_changed(base: str, *, verify_git: bool = False, root: Path = ROOT) -> list[Finding]:
    findings: list[Finding] = []
    referenced: set[Path] = set()
    known = load_mistake_ids(root)
    if not known:
        findings.append(Finding("FAIL", root / MISTAKE_LEDGER.relative_to(ROOT), 1, "mistake ledger missing/no IDs"))
    changed = git_changed_paths(base, root)
    changed_logs: set[Path] = set()
    for raw in changed:
        path = root / raw
        if raw.startswith("docs/prompt_queues/") and raw.endswith(".md") and path.exists():
            touched = changed_line_numbers(path, base, root)
            for line_no, line in enumerate(read_text(path).splitlines(), 1):
                if line_no in touched:
                    row_findings, row_logs = validate_queue_row(path, line_no, line, root)
                    findings.extend(row_findings)
                    referenced.update(row_logs)
        if raw.startswith(".ai/runs/") and raw.endswith(".md") and not raw.endswith("README.md") and path.exists():
            changed_logs.add(path)
    for log in sorted(changed_logs | referenced):
        if log.exists():
            findings.extend(validate_run_log(log, known, referenced | changed_logs, verify_git=verify_git, root=root))
    return findings


def validate_all(*, strict_legacy: bool = False, referenced_only: bool = False, verify_git: bool = False, root: Path = ROOT) -> list[Finding]:
    findings: list[Finding] = []
    referenced: set[Path] = set()
    known = load_mistake_ids(root)
    if not known:
        findings.append(Finding("FAIL", root / MISTAKE_LEDGER.relative_to(ROOT), 1, "mistake ledger missing/no IDs"))
    for queue in iter_queue_files(root):
        for line_no, line in enumerate(read_text(queue).splitlines(), 1):
            row_findings, row_logs = validate_queue_row(queue, line_no, line, root)
            if strict_legacy:
                row_findings = [Finding("FAIL", item.path, item.line_no, item.message) for item in row_findings]
            findings.extend(row_findings)
            referenced.update(row_logs)
    logs = referenced if referenced_only else set(iter_run_logs(root))
    for log in sorted(logs):
        if log.exists():
            log_findings = validate_run_log(log, known, referenced, verify_git=verify_git, root=root)
            if strict_legacy:
                log_findings = [Finding("FAIL", item.path, item.line_no, item.message) for item in log_findings]
            findings.extend(log_findings)
    return findings


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--changed-from")
    parser.add_argument("--referenced-run-logs-only", action="store_true")
    parser.add_argument("--strict-legacy", action="store_true")
    parser.add_argument("--verify-git", action="store_true")
    args = parser.parse_args(argv)
    if args.changed_from:
        findings = validate_changed(args.changed_from, verify_git=args.verify_git)
        scope = f"changed-from {args.changed_from}"
    else:
        findings = validate_all(
            strict_legacy=args.strict_legacy,
            referenced_only=args.referenced_run_logs_only,
            verify_git=args.verify_git,
        )
        scope = "referenced" if args.referenced_run_logs_only else "all"
    failures = [item for item in findings if item.severity == "FAIL"]
    warnings = [item for item in findings if item.severity == "WARN"]
    print(f"Agent evidence validation: scope={scope} failures={len(failures)} warnings={len(warnings)}")
    for finding in findings:
        print(finding.format())
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
