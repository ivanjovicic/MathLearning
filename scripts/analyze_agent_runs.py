#!/usr/bin/env python3
"""Summarize agent-run throughput debt and flag regressions in changed v2 logs."""
from __future__ import annotations

import argparse
import json
import re
import subprocess
from collections import Counter
from dataclasses import asdict, dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUNS = ROOT / ".ai" / "runs"
MIXED_LANE_RE = re.compile(r"\b(?:implementation|known-fix|audit|tests|validation-only)\s*[+/,&]\s*", re.I)


@dataclass(frozen=True)
class RunMetric:
    path: str
    format: str
    lines: int
    unknown_elapsed: bool
    mixed_lane: bool
    files_inspected: int | None
    files_changed: int | None
    searches: int | None
    completion: int | None
    waste: str


def field(text: str, name: str) -> str | None:
    match = re.search(rf"^\s*{re.escape(name)}\s*:\s*(.*)$", text, re.I | re.M)
    return match.group(1).strip() if match else None


def number(text: str, name: str) -> int | None:
    value = field(text, name)
    return int(value) if value and value.isdigit() else None


def section(text: str, heading: str) -> str:
    match = re.search(rf"^##\s+{re.escape(heading)}\s*$", text, re.I | re.M)
    if not match:
        return ""
    tail = text[match.end():]
    boundary = re.search(r"^##\s+", tail, re.M)
    return tail[: boundary.start()] if boundary else tail


def metric(path: Path, root: Path = ROOT) -> RunMetric:
    text = path.read_text(encoding="utf-8", errors="replace")
    fmt = (field(text, "Evidence format") or "legacy").casefold()
    elapsed = field(text, "Elapsed time") or ""
    lane = field(text, "Run mode") or ""
    completion = number(text, "Completion %")
    if completion is None:
        match = re.search(r"Completion %\s*\n\s*[-*]?\s*(\d{1,3})", text, re.I)
        completion = int(match.group(1)) if match else None
    waste = field(text, "Waste") or section(text, "Waste categories").strip().replace("\n", " ")
    try:
        shown = path.relative_to(root).as_posix()
    except ValueError:
        shown = path.as_posix()
    return RunMetric(
        path=shown,
        format=fmt,
        lines=len(text.splitlines()),
        unknown_elapsed=not elapsed or "unknown" in elapsed.casefold() or elapsed.casefold() == "open",
        mixed_lane=bool(MIXED_LANE_RE.search(lane)),
        files_inspected=number(text, "Files inspected"),
        files_changed=number(text, "Files changed"),
        searches=number(text, "Searches"),
        completion=completion,
        waste=waste or "none",
    )


def changed_logs(base: str, root: Path = ROOT) -> list[Path]:
    result = subprocess.run(
        ["git", "diff", "--diff-filter=AM", "--name-only", f"{base}...HEAD"],
        cwd=root, text=True, capture_output=True, check=True, timeout=30,
    )
    return [
        root / line.strip()
        for line in result.stdout.splitlines()
        if line.startswith(".ai/runs/") and line.endswith(".md") and not line.endswith("README.md")
    ]


def collect(paths: list[Path] | None = None, root: Path = ROOT) -> list[RunMetric]:
    selected = paths if paths is not None else sorted((root / ".ai/runs").glob("*.md"))
    return [metric(path, root) for path in selected if path.exists() and path.name != "README.md"]


def summarize(metrics: list[RunMetric]) -> dict[str, object]:
    wastes = Counter()
    for item in metrics:
        if item.waste and item.waste.casefold() != "none":
            for token in re.split(r"[;|,]", item.waste):
                cleaned = re.sub(r"\s+", " ", token.strip(" -\n"))
                if cleaned:
                    wastes[cleaned[:100]] += 1
    return {
        "runs": len(metrics),
        "v2": sum(item.format == "v2" for item in metrics),
        "legacy": sum(item.format != "v2" for item in metrics),
        "unknown_elapsed": sum(item.unknown_elapsed for item in metrics),
        "mixed_lane": sum(item.mixed_lane for item in metrics),
        "over_120_lines": sum(item.lines > 120 for item in metrics),
        "missing_numeric_metrics": sum(
            item.format == "v2" and None in (item.files_inspected, item.files_changed, item.searches)
            for item in metrics
        ),
        "top_waste": wastes.most_common(8),
    }


def regression_findings(metrics: list[RunMetric]) -> list[str]:
    findings: list[str] = []
    for item in metrics:
        if item.format != "v2":
            findings.append(f"{item.path}: changed run log must use Evidence format v2")
        if item.unknown_elapsed:
            findings.append(f"{item.path}: elapsed time is not recorded")
        if item.mixed_lane:
            findings.append(f"{item.path}: mixed run lane")
        if item.lines > 90 and item.format == "v2":
            findings.append(f"{item.path}: v2 log is {item.lines} lines; target <=90")
        if item.format == "v2" and None in (item.files_inspected, item.files_changed, item.searches):
            findings.append(f"{item.path}: missing numeric throughput metrics")
    return findings


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--changed-from")
    parser.add_argument("--recent", type=int)
    parser.add_argument("--json", action="store_true")
    parser.add_argument("--fail-on-regression", action="store_true")
    args = parser.parse_args(argv)
    paths = changed_logs(args.changed_from) if args.changed_from else None
    metrics = collect(paths)
    if args.recent:
        metrics = metrics[-args.recent:]
    summary = summarize(metrics)
    findings = regression_findings(metrics) if args.fail_on_regression else []
    if args.json:
        print(json.dumps({"summary": summary, "findings": findings, "runs": [asdict(item) for item in metrics]}, indent=2))
    else:
        print(
            "Agent run speed summary: "
            f"runs={summary['runs']} v2={summary['v2']} legacy={summary['legacy']} "
            f"unknown_elapsed={summary['unknown_elapsed']} mixed_lane={summary['mixed_lane']} "
            f"over_120_lines={summary['over_120_lines']}"
        )
        for waste, count in summary["top_waste"]:
            print(f"waste[{count}]: {waste}")
        for finding in findings:
            print(f"[FAIL] {finding}")
    return 1 if findings else 0


if __name__ == "__main__":
    raise SystemExit(main())
