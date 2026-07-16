#!/usr/bin/env python3
"""Classify whether a change needs the expensive .NET/PostgreSQL validation suite."""
from __future__ import annotations

import argparse
import fnmatch
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
ZERO_SHA = "0" * 40
RUNTIME_PATTERNS = (
    "src/**",
    "tests/**",
    "scripts/db/**",
    "*.sln",
    "*.slnx",
    "*.csproj",
    "*.props",
    "*.targets",
    "Directory.Build.*",
    "Directory.Packages.*",
    "global.json",
    "NuGet.config",
    "Dockerfile",
    "docker-compose*.yml",
    "docker-compose*.yaml",
)


def normalize(path: str) -> str:
    return path.strip().replace("\\", "/").lstrip("./")


def matches(path: str, pattern: str) -> bool:
    path = normalize(path)
    if pattern.endswith("/**"):
        return path.startswith(pattern[:-3])
    return fnmatch.fnmatch(path, pattern)


def requires_database_validation(paths: list[str]) -> tuple[bool, list[str]]:
    matched: list[str] = []
    for raw in paths:
        path = normalize(raw)
        if any(matches(path, pattern) for pattern in RUNTIME_PATTERNS):
            matched.append(path)
    return bool(matched), matched


def changed_paths(base: str, head: str, root: Path = ROOT) -> list[str]:
    if not base or base == ZERO_SHA or not head:
        raise ValueError("base/head unavailable")
    result = subprocess.run(
        ["git", "diff", "--diff-filter=AMDR", "--name-only", base, head],
        cwd=root,
        text=True,
        capture_output=True,
        check=True,
        timeout=30,
    )
    return [normalize(line) for line in result.stdout.splitlines() if line.strip()]


def write_github_output(path: Path, required: bool, reason: str) -> None:
    with path.open("a", encoding="utf-8") as handle:
        handle.write(f"database_validation={'true' if required else 'false'}\n")
        handle.write(f"reason={reason.replace(chr(10), ' ')}\n")


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base")
    parser.add_argument("--head")
    parser.add_argument("--path", action="append", default=[])
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--github-output")
    return parser


def main(argv: list[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    if args.force:
        required, reason = True, "forced by workflow dispatch"
        paths: list[str] = args.path
    else:
        try:
            paths = args.path or changed_paths(args.base or "", args.head or "")
            required, matched = requires_database_validation(paths)
            reason = (
                "runtime/database paths changed: " + ", ".join(matched[:8])
                if required
                else "docs/agent-tooling-only change; expensive database suite skipped"
            )
        except (OSError, subprocess.SubprocessError, ValueError) as exc:
            paths = args.path
            required, reason = True, f"safe fallback because change scope could not be resolved: {exc}"
    print(f"database_validation={'true' if required else 'false'}")
    print(f"reason={reason}")
    if paths:
        print(f"changed_paths={len(paths)}")
    if args.github_output:
        write_github_output(Path(args.github_output), required, reason)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
