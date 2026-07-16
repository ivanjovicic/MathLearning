#!/usr/bin/env python3
"""Create compact backend agent plans and evidence logs without manual boilerplate."""
from __future__ import annotations

import argparse
import json
import re
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[1]
INDEX_PATH = ROOT / "docs" / "ai" / "learning" / "MISTAKE_INDEX.json"
BUDGETS = {
    "micro": {"minutes": 8, "workflow_reads": 2, "source_reads": 4, "changed": 2, "searches": 1},
    "low": {"minutes": 15, "workflow_reads": 3, "source_reads": 8, "changed": 3, "searches": 2},
    "medium": {"minutes": 30, "workflow_reads": 5, "source_reads": 15, "changed": 6, "searches": 4},
    "high": {"minutes": 30, "workflow_reads": 8, "source_reads": 20, "changed": 10, "searches": 6},
}
ALLOWED_LANES = (
    "known-fix",
    "investigation",
    "validation-only",
    "tests",
    "docs-evidence",
    "audit",
    "review",
)
OPEN = "open"


def utc_now() -> datetime:
    return datetime.now(timezone.utc).replace(microsecond=0)


def iso(value: datetime) -> str:
    return value.isoformat().replace("+00:00", "Z")


def load_index(path: Path | None = None) -> dict[str, Any]:
    selected = path or INDEX_PATH
    data = json.loads(selected.read_text(encoding="utf-8"))
    if data.get("version") != 1:
        raise ValueError("unsupported mistake index version")
    return data


def merge_plan(index: dict[str, Any], areas: list[str]) -> dict[str, list[str]]:
    selected = areas or ["default"]
    mistakes: list[str] = []
    reads: list[str] = []
    proof: list[str] = []

    def add_unique(target: list[str], values: list[str]) -> None:
        for value in values:
            if value not in target:
                target.append(value)

    add_unique(mistakes, index["default"].get("mistakes", []))
    add_unique(reads, index["default"].get("reads", []))
    add_unique(proof, index["default"].get("proof", []))
    for area in selected:
        if area == "default":
            continue
        entry = index.get("areas", {}).get(area)
        if entry is None:
            known = ", ".join(sorted(index.get("areas", {})))
            raise ValueError(f"unknown area '{area}'. Known areas: {known}")
        add_unique(mistakes, entry.get("mistakes", []))
        add_unique(reads, entry.get("reads", []))
        add_unique(proof, entry.get("proof", []))
    return {"mistakes": mistakes, "reads": reads, "proof": proof}


def plan_text(*, lane: str, budget: str, areas: list[str], index: dict[str, Any]) -> str:
    limits = BUDGETS[budget]
    plan = merge_plan(index, areas)
    lines = [
        f"Lane: {lane}",
        f"Budget: {budget} ({limits['minutes']} minutes)",
        (
            "Limits: "
            f"workflow_reads={limits['workflow_reads']}; source_reads={limits['source_reads']}; "
            f"changed={limits['changed']}; searches={limits['searches']}"
        ),
        "Relevant mistakes: " + (", ".join(plan["mistakes"]) or "none"),
        "Read first:",
    ]
    lines.extend(f"- {item}" for item in plan["reads"])
    lines.append("Focused proof:")
    lines.extend(f"- {item}" for item in plan["proof"])
    lines.append("Stop: second subsystem, second falsifier, repeated unchanged failure, unavailable proof or budget limit.")
    return "\n".join(lines)


def default_log_path(prompt_id: str, now: datetime) -> Path:
    safe_id = re.sub(r"[^A-Za-z0-9_-]+", "-", prompt_id).strip("-")
    return ROOT / ".ai" / "runs" / f"{now.date().isoformat()}-{safe_id}-evidence.md"


def render_log(args: argparse.Namespace, now: datetime, plan: dict[str, list[str]]) -> str:
    mistakes = ", ".join(plan["mistakes"]) or "none"
    prevention = "; ".join(f"apply {item}" for item in plan["mistakes"]) if plan["mistakes"] else "none"
    return f"""# {args.prompt_id} Evidence

Evidence format: v2
Prompt ID: {args.prompt_id}
Queue: {args.queue}
Agent/tool: {args.agent_tool}
Model provider: {args.model_provider}
Model name/id: {args.model_name}
Client/IDE: {args.client}
Run mode: {args.lane}
Token budget: {args.budget}
Started at UTC: {iso(now)}
Completed at UTC: {OPEN}
Elapsed time: {OPEN}
Relevant prior mistakes read: {mistakes}
How this run avoids prior mistakes: {prevention}
Owner/hypothesis: {args.owner_hypothesis}
Files inspected: 0
Files changed: 0
Searches: 0
Validation runs: 0
Failed retries: 0

## Outcome
- {OPEN}

## Changed paths
- {OPEN}

## Validation
Validation run: {OPEN}
Validation not run: {OPEN}

## Exceptions and learning
Mistakes observed: {OPEN}
Waste: {OPEN}
Missed: {OPEN}
Follow-up: {OPEN}
Residual risk: {OPEN}
Documentation impact: {OPEN}
Cross-repo impact: {OPEN}

## Delivery
State: In progress
Branch/PR: {OPEN}
Commit SHA: self
Completion %: 0
"""


def replace_field(text: str, name: str, value: str) -> str:
    pattern = re.compile(rf"^{re.escape(name)}:\s*.*$", re.MULTILINE)
    if not pattern.search(text):
        raise ValueError(f"field not found: {name}")
    return pattern.sub(f"{name}: {value}", text, count=1)


def replace_section(text: str, heading: str, lines: list[str]) -> str:
    pattern = re.compile(rf"(^##\s+{re.escape(heading)}\s*$)(.*?)(?=^##\s+|\Z)", re.MULTILINE | re.DOTALL)
    match = pattern.search(text)
    if not match:
        raise ValueError(f"section not found: {heading}")
    body = "\n" + "\n".join(lines).rstrip() + "\n\n"
    return text[: match.start()] + match.group(1) + body + text[match.end() :]


def parse_utc(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def get_field(text: str, name: str) -> str:
    match = re.search(rf"^{re.escape(name)}:\s*(.*)$", text, re.MULTILINE)
    if not match:
        raise ValueError(f"field not found: {name}")
    return match.group(1).strip()


def finish_log(args: argparse.Namespace) -> Path:
    path = Path(args.log).resolve()
    text = path.read_text(encoding="utf-8")
    if get_field(text, "Evidence format") != "v2":
        raise ValueError("finish supports Evidence format: v2 logs only")
    started = parse_utc(get_field(text, "Started at UTC"))
    completed = utc_now()
    elapsed_seconds = max(0, int((completed - started).total_seconds()))
    elapsed = f"{elapsed_seconds // 60}m {elapsed_seconds % 60}s"

    fields = {
        "Completed at UTC": iso(completed),
        "Elapsed time": elapsed,
        "Files inspected": str(args.inspected),
        "Files changed": str(args.changed),
        "Searches": str(args.searches),
        "Validation runs": str(args.validation_runs),
        "Failed retries": str(args.failed_retries),
        "Mistakes observed": args.mistakes,
        "Waste": args.waste,
        "Missed": args.missed,
        "Follow-up": args.follow_up,
        "Residual risk": args.residual_risk,
        "Documentation impact": args.documentation_impact,
        "Cross-repo impact": args.cross_repo_impact,
        "State": args.state,
        "Branch/PR": args.branch_pr,
        "Completion %": str(args.completion),
    }
    for name, value in fields.items():
        text = replace_field(text, name, value)

    outcomes = args.outcome or ["none"]
    changed_paths = args.changed_path or ["none"]
    validations = args.validation or ["not run - no validation supplied"]
    validation_not_run = args.validation_not_run or ["none"]
    text = replace_section(text, "Outcome", [f"- {item}" for item in outcomes])
    text = replace_section(text, "Changed paths", [f"- {item}" for item in changed_paths])
    text = replace_section(
        text,
        "Validation",
        [
            "Validation run: " + " | ".join(validations),
            "Validation not run: " + " | ".join(validation_not_run),
        ],
    )
    path.write_text(text, encoding="utf-8")
    return path


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="command", required=True)

    plan = sub.add_parser("plan", help="Print the minimal read/proof packet for a task area.")
    plan.add_argument("--area", action="append", default=[])
    plan.add_argument("--lane", choices=ALLOWED_LANES, default="known-fix")
    plan.add_argument("--budget", choices=tuple(BUDGETS), default="low")

    start = sub.add_parser("start", help="Create a compact Evidence format v2 run log.")
    start.add_argument("--prompt-id", required=True)
    start.add_argument("--queue", default="user-assigned")
    start.add_argument("--area", action="append", default=[])
    start.add_argument("--lane", choices=ALLOWED_LANES, default="known-fix")
    start.add_argument("--budget", choices=tuple(BUDGETS), default="low")
    start.add_argument("--output")
    start.add_argument("--agent-tool", default="unknown-not-exposed")
    start.add_argument("--model-provider", default="unknown-not-exposed")
    start.add_argument("--model-name", default="unknown-not-exposed")
    start.add_argument("--client", default="unknown-not-exposed")
    start.add_argument("--owner-hypothesis", default="open")

    finish = sub.add_parser("finish", help="Close a compact run log and calculate elapsed time.")
    finish.add_argument("log")
    finish.add_argument("--state", default="Needs merge")
    finish.add_argument("--completion", type=int, required=True)
    finish.add_argument("--inspected", type=int, required=True)
    finish.add_argument("--changed", type=int, required=True)
    finish.add_argument("--searches", type=int, required=True)
    finish.add_argument("--validation-runs", type=int, required=True)
    finish.add_argument("--failed-retries", type=int, default=0)
    finish.add_argument("--outcome", action="append")
    finish.add_argument("--changed-path", action="append")
    finish.add_argument("--validation", action="append")
    finish.add_argument("--validation-not-run", action="append")
    finish.add_argument("--mistakes", default="none")
    finish.add_argument("--waste", default="none")
    finish.add_argument("--missed", default="none")
    finish.add_argument("--follow-up", default="none")
    finish.add_argument("--residual-risk", default="none")
    finish.add_argument("--documentation-impact", default="none")
    finish.add_argument("--cross-repo-impact", default="no")
    finish.add_argument("--branch-pr", default="unknown")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    index = load_index()
    if args.command == "plan":
        print(plan_text(lane=args.lane, budget=args.budget, areas=args.area, index=index))
        return 0
    if args.command == "start":
        now = utc_now()
        plan = merge_plan(index, args.area)
        path = Path(args.output).resolve() if args.output else default_log_path(args.prompt_id, now)
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists():
            raise FileExistsError(f"run log already exists: {path}")
        path.write_text(render_log(args, now, plan), encoding="utf-8")
        print(path.relative_to(ROOT) if path.is_relative_to(ROOT) else path)
        print(plan_text(lane=args.lane, budget=args.budget, areas=args.area, index=index))
        return 0
    path = finish_log(args)
    print(path.relative_to(ROOT) if path.is_relative_to(ROOT) else path)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
