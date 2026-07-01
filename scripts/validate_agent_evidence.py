#!/usr/bin/env python3
"""Validate backend agent evidence rows and run logs.

Docs/tooling-only checker for the rules in:

- docs/AGENT_SHARED_OPERATING_STANDARD.md
- docs/AGENT_RUN_LOG_ENFORCEMENT.md
- docs/ai/prompts/RUN_LOG_EVIDENCE_LINT_PROMPT.md
- docs/ai/learning/MISTAKE_LEDGER.md

Run from repository root:

    python scripts/validate_agent_evidence.py

Behavior:
- FAIL for clear violations in modern/high-confidence Done rows.
- FAIL for referenced run logs that are missing or internally inconsistent.
- WARN for older/legacy rows/logs where the repository historically allowed less evidence.
- Never guesses model names, elapsed time, token counts, validation results, or CI proof.
"""

from __future__ import annotations

import argparse
import re
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

ROOT = Path(__file__).resolve().parents[1]
QUEUE_DIR = ROOT / "docs" / "prompt_queues"
RUNS_DIR = ROOT / ".ai" / "runs"
MISTAKE_LEDGER = ROOT / "docs" / "ai" / "learning" / "MISTAKE_LEDGER.md"

STRICT_GATE_DATE = "2026-07-01"

MISSING_PROOF_WORDS = (
    "missing",
    "not run",
    "not verified",
    "not implemented",
    "not created",
    "target evidence",
    "evidence gap",
    "no run log",
    "without run-log",
    "without run log",
)

DONE_RE = re.compile(r"\bDone\b(?:\s+(\d{1,3})%)?", re.IGNORECASE)
DATE_RE = re.compile(r"20\d{2}-\d{2}-\d{2}")
RUN_LOG_RE = re.compile(r"Run log:\s*([^;|)]+)", re.IGNORECASE)
RUN_PATH_RE = re.compile(r"\.ai/runs/[A-Za-z0-9_./\-]+\.md")
MISTAKE_ID_RE = re.compile(r"\bBACKEND-MISTAKE-[A-Z0-9]+(?:-[A-Z0-9]+)*\b")
COMPLETION_LOG_RE = re.compile(r"Completion %\s*\n\s*(?:[-*]\s*)?(\d{1,3})", re.IGNORECASE)


@dataclass(frozen=True)
class Finding:
    severity: str
    path: Path
    line_no: int
    message: str

    def format(self) -> str:
        try:
            rel = self.path.relative_to(ROOT)
        except ValueError:
            rel = self.path
        return f"[{self.severity}] {rel}:{self.line_no} - {self.message}"


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8", errors="replace")


def iter_queue_files() -> Iterable[Path]:
    if not QUEUE_DIR.exists():
        return []
    return sorted(QUEUE_DIR.glob("*.md"))


def iter_run_logs() -> Iterable[Path]:
    if not RUNS_DIR.exists():
        return []
    return sorted(RUNS_DIR.glob("*.md"))


def load_mistake_ids() -> set[str]:
    if not MISTAKE_LEDGER.exists():
        return set()
    return set(MISTAKE_ID_RE.findall(read_text(MISTAKE_LEDGER)))


def extract_date(line: str) -> str | None:
    match = DATE_RE.search(line)
    return match.group(0) if match else None


def is_modern_row(line: str) -> bool:
    date = extract_date(line)
    if date and date >= STRICT_GATE_DATE:
        return True
    lowered = line.lower()
    return "evidence gap" in lowered or "run log:" in lowered


def is_strict_log(text: str, path: Path, referenced_logs: set[Path]) -> bool:
    if path in referenced_logs:
        return True
    markers = (
        "Relevant prior mistakes read:",
        "How this run avoids prior mistakes:",
        "## Mistakes observed",
    )
    return any(marker in text for marker in markers)


def is_table_row(line: str) -> bool:
    stripped = line.strip()
    return stripped.startswith("|") and stripped.endswith("|") and "|---" not in stripped


def first_cell(line: str) -> str:
    parts = [part.strip() for part in line.strip().strip("|").split("|")]
    return parts[0] if parts else "unknown"


def has_casefold(text: str, needle: str) -> bool:
    return needle.casefold() in text.casefold()


def missing_done_row_fields(line: str) -> list[str]:
    required = [
        "Model:",
        "Run log:",
        "Mistakes:",
        "Waste:",
        "Missed:",
        "Follow-up:",
        "Residual risk:",
    ]
    return [field for field in required if not has_casefold(line, field)]


def extract_run_log_paths(line: str) -> list[str]:
    return RUN_PATH_RE.findall(line)


def row_has_run_log_fallback(line: str) -> bool:
    match = RUN_LOG_RE.search(line)
    if not match:
        return False
    value = match.group(1).strip().casefold()
    return value.startswith("fallback")


def row_score(line: str) -> int | None:
    match = DONE_RE.search(line)
    if not match or match.group(1) is None:
        return None
    return int(match.group(1))


def residual_risk_text(line: str) -> str:
    lower = line.casefold()
    idx = lower.find("residual risk:")
    return line[idx:] if idx >= 0 else line


def row_has_missing_proof_but_claims_100(line: str) -> bool:
    score = row_score(line)
    if score != 100:
        return False
    risk = residual_risk_text(line).casefold()
    return any(word in risk for word in MISSING_PROOF_WORDS)


def validate_queue_row(path: Path, line_no: int, line: str) -> tuple[list[Finding], set[Path]]:
    findings: list[Finding] = []
    referenced_logs: set[Path] = set()
    if not is_table_row(line) or not DONE_RE.search(line):
        return findings, referenced_logs

    severity = "FAIL" if is_modern_row(line) else "WARN"
    prompt_id = first_cell(line)

    missing = missing_done_row_fields(line)
    if missing:
        findings.append(
            Finding(
                severity,
                path,
                line_no,
                f"{prompt_id} Done row is missing required evidence fields: {', '.join(missing)}",
            )
        )

    if "Run log:" in line and not row_has_run_log_fallback(line):
        paths = extract_run_log_paths(line)
        if not paths:
            findings.append(Finding(severity, path, line_no, f"{prompt_id} has Run log field but no .ai/runs/*.md path"))
        for run_path in paths:
            target = ROOT / run_path
            referenced_logs.add(target)
            if not target.exists():
                findings.append(Finding("FAIL", path, line_no, f"{prompt_id} references missing run log: {run_path}"))

    if row_has_missing_proof_but_claims_100(line):
        findings.append(
            Finding("FAIL", path, line_no, f"{prompt_id} claims 100% while residual risk/evidence text says proof is missing")
        )

    return findings, referenced_logs


def field_exists(text: str, field_name: str) -> bool:
    return re.search(rf"^\s*{re.escape(field_name)}\s*:", text, re.IGNORECASE | re.MULTILINE) is not None


def field_has_value_or_placeholder(text: str, field_name: str, placeholders: tuple[str, ...]) -> bool:
    match = re.search(rf"^\s*{re.escape(field_name)}\s*:\s*(.*)$", text, re.IGNORECASE | re.MULTILINE)
    if not match:
        return False
    value = match.group(1).strip()
    return bool(value) or any(token in text for token in placeholders)


def section_text(text: str, heading: str) -> str:
    pattern = re.compile(rf"^##\s+{re.escape(heading)}\s*$", re.IGNORECASE | re.MULTILINE)
    match = pattern.search(text)
    if not match:
        return ""
    start = match.end()
    next_heading = re.search(r"^##\s+", text[start:], re.MULTILINE)
    end = start + next_heading.start() if next_heading else len(text)
    return text[start:end]


def log_completion_score(text: str) -> int | None:
    match = COMPLETION_LOG_RE.search(text)
    return int(match.group(1)) if match else None


def validate_run_log(path: Path, known_mistakes: set[str], referenced_logs: set[Path]) -> list[Finding]:
    findings: list[Finding] = []
    text = read_text(path)
    strict = is_strict_log(text, path, referenced_logs)
    severity = "FAIL" if strict else "WARN"

    required_fields = [
        "Prompt ID",
        "Queue",
        "Agent/tool",
        "Model provider",
        "Model name/id",
        "Model mode/settings",
        "Client/IDE",
        "Run mode",
        "Token budget",
        "Elapsed time",
        "Phase time breakdown",
        "Commit SHA",
    ]
    for field in required_fields:
        if not field_exists(text, field):
            findings.append(Finding(severity, path, 1, f"run log missing required field: {field}:"))

    if field_exists(text, "Model name/id") and not field_has_value_or_placeholder(text, "Model name/id", ("unknown-not-exposed",)):
        findings.append(Finding("FAIL" if strict else "WARN", path, 1, "Model name/id must be present or explicitly unknown-not-exposed"))

    for field in ("Elapsed time", "Phase time breakdown"):
        if field_exists(text, field) and not field_has_value_or_placeholder(text, field, ("unknown-not-recorded",)):
            findings.append(Finding("FAIL" if strict else "WARN", path, 1, f"{field} must be present or explicitly unknown-not-recorded"))

    if "## Mistakes observed" not in text:
        findings.append(Finding(severity, path, 1, "run log missing ## Mistakes observed section"))
    else:
        mistakes_section = section_text(text, "Mistakes observed")
        if "none" not in mistakes_section.casefold():
            mistake_ids = set(MISTAKE_ID_RE.findall(mistakes_section))
            if not mistake_ids:
                findings.append(Finding("FAIL" if strict else "WARN", path, 1, "Mistakes observed is not 'none' and has no BACKEND-MISTAKE-* ID"))
            unknown = sorted(mistake_ids - known_mistakes)
            for mistake_id in unknown:
                findings.append(Finding("FAIL", path, 1, f"unknown mistake ID referenced: {mistake_id}"))
            if "repeated" in mistakes_section.casefold():
                required_repeat_phrases = (
                    "Prevention added:",
                    "Did this run update a rule/prompt/test/queue:",
                )
                missing_repeat = [phrase for phrase in required_repeat_phrases if phrase not in mistakes_section]
                if missing_repeat:
                    findings.append(
                        Finding(
                            "FAIL" if strict else "WARN",
                            path,
                            1,
                            "repeated mistake section missing prevention/update fields: " + ", ".join(missing_repeat),
                        )
                    )

    score = log_completion_score(text)
    residual = section_text(text, "Residual risk").casefold()
    if score == 100 and any(word in residual for word in MISSING_PROOF_WORDS):
        findings.append(Finding("FAIL", path, 1, "run log claims 100% while residual risk says proof/evidence is missing"))

    return findings


def validate_all(strict_legacy: bool = False, all_run_logs: bool = True) -> list[Finding]:
    findings: list[Finding] = []
    referenced_logs: set[Path] = set()
    known_mistakes = load_mistake_ids()

    if not known_mistakes:
        findings.append(Finding("FAIL", MISTAKE_LEDGER, 1, "mistake ledger missing or contains no BACKEND-MISTAKE-* IDs"))

    for queue_file in iter_queue_files():
        for idx, line in enumerate(read_text(queue_file).splitlines(), start=1):
            row_findings, row_logs = validate_queue_row(queue_file, idx, line)
            referenced_logs.update(row_logs)
            if strict_legacy:
                row_findings = [Finding("FAIL", f.path, f.line_no, f.message) for f in row_findings]
            findings.extend(row_findings)

    if RUNS_DIR.exists():
        logs_to_check = set(iter_run_logs()) if all_run_logs else referenced_logs
        for run_log in sorted(logs_to_check):
            if run_log.exists():
                log_findings = validate_run_log(run_log, known_mistakes, referenced_logs)
                if strict_legacy:
                    log_findings = [Finding("FAIL", f.path, f.line_no, f.message) for f in log_findings]
                findings.extend(log_findings)
    else:
        findings.append(Finding("FAIL", RUNS_DIR, 1, ".ai/runs directory does not exist"))

    return findings


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Validate agent queue rows and .ai/runs evidence logs.")
    parser.add_argument("--strict-legacy", action="store_true", help="Treat legacy warnings as failures.")
    parser.add_argument(
        "--referenced-run-logs-only",
        action="store_true",
        help="Validate only run logs referenced by queue rows, instead of scanning all .ai/runs logs.",
    )
    args = parser.parse_args(argv)

    findings = validate_all(strict_legacy=args.strict_legacy, all_run_logs=not args.referenced_run_logs_only)
    failures = [f for f in findings if f.severity == "FAIL"]
    warnings = [f for f in findings if f.severity == "WARN"]

    print("Agent evidence validation")
    print(f"Root: {ROOT}")
    print(f"Queue files: {len(list(iter_queue_files()))}")
    print(f"Run logs scanned: {'all' if not args.referenced_run_logs_only else 'referenced only'}")
    print(f"Failures: {len(failures)}")
    print(f"Warnings: {len(warnings)}")

    if findings:
        print("\nFindings:")
        for finding in findings:
            print("- " + finding.format())

    if failures:
        print("\nResult: FAIL")
        return 1

    print("\nResult: PASS")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
