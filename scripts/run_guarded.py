#!/usr/bin/env python3
"""Run one command with wall-clock and idle-output limits, killing its process tree."""
from __future__ import annotations

import argparse
import json
import os
import queue
import signal
import subprocess
import sys
import threading
import time
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Sequence

DEFAULT_TIMEOUT_SECONDS = 180
DEFAULT_IDLE_TIMEOUT_SECONDS = 90
DEFAULT_GRACE_SECONDS = 3
MAX_TIMEOUT_SECONDS = 300
MAX_IDLE_TIMEOUT_SECONDS = 120
POLL_SECONDS = 0.1
FORBIDDEN_TOKENS = {"&&", "||", ";"}


@dataclass(frozen=True)
class GuardResult:
    command: list[str]
    cwd: str
    duration_seconds: float
    exit_code: int
    outcome: str
    timeout_seconds: int
    idle_timeout_seconds: int


def _validate_limits(timeout_seconds: int, idle_timeout_seconds: int, grace_seconds: int) -> None:
    if not 1 <= timeout_seconds <= MAX_TIMEOUT_SECONDS:
        raise ValueError(f"timeout must be between 1 and {MAX_TIMEOUT_SECONDS} seconds")
    if not 1 <= idle_timeout_seconds <= min(timeout_seconds, MAX_IDLE_TIMEOUT_SECONDS):
        raise ValueError("idle timeout is outside the allowed range")
    if not 0 <= grace_seconds <= 10:
        raise ValueError("grace period must be between 0 and 10 seconds")


def _validate_command(command: Sequence[str]) -> list[str]:
    normalized = [str(part) for part in command if str(part)]
    if not normalized:
        raise ValueError("a command is required after --")
    if any(part in FORBIDDEN_TOKENS for part in normalized):
        raise ValueError("command chaining tokens are forbidden")
    if any("\n" in part or "\r" in part for part in normalized):
        raise ValueError("command arguments must not contain newlines")
    return normalized


def _reader(stream, events: queue.Queue[tuple[float, str] | None]) -> None:
    try:
        for line in iter(stream.readline, ""):
            events.put((time.monotonic(), line))
    finally:
        events.put(None)


def _terminate_process_tree(process: subprocess.Popen[str], grace_seconds: int) -> None:
    if process.poll() is not None:
        return
    if os.name == "nt":
        try:
            subprocess.run(
                ["taskkill", "/PID", str(process.pid), "/T", "/F"],
                check=False,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                timeout=max(1, grace_seconds + 2),
            )
            return
        except (OSError, subprocess.TimeoutExpired):
            pass
    else:
        try:
            process_group = os.getpgid(process.pid)
            os.killpg(process_group, signal.SIGTERM)
            try:
                process.wait(timeout=grace_seconds)
                return
            except subprocess.TimeoutExpired:
                os.killpg(process_group, signal.SIGKILL)
                return
        except (OSError, ProcessLookupError):
            pass
    try:
        process.kill()
    except OSError:
        pass


def run_guarded(
    command: Sequence[str],
    *,
    timeout_seconds: int = DEFAULT_TIMEOUT_SECONDS,
    idle_timeout_seconds: int = DEFAULT_IDLE_TIMEOUT_SECONDS,
    grace_seconds: int = DEFAULT_GRACE_SECONDS,
    cwd: str | Path | None = None,
    json_output: str | Path | None = None,
) -> GuardResult:
    _validate_limits(timeout_seconds, idle_timeout_seconds, grace_seconds)
    normalized = _validate_command(command)
    working_directory = str(Path(cwd or Path.cwd()).resolve())
    creationflags = 0
    popen_kwargs: dict[str, object] = {}
    if os.name == "nt":
        creationflags = subprocess.CREATE_NEW_PROCESS_GROUP
    else:
        popen_kwargs["start_new_session"] = True

    started = time.monotonic()
    last_output = started
    process = subprocess.Popen(
        normalized,
        cwd=working_directory,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        bufsize=1,
        creationflags=creationflags,
        **popen_kwargs,
    )
    assert process.stdout is not None

    events: queue.Queue[tuple[float, str] | None] = queue.Queue()
    reader = threading.Thread(target=_reader, args=(process.stdout, events), daemon=True)
    reader.start()
    outcome = "completed"
    exit_code: int | None = None

    while True:
        try:
            event = events.get(timeout=POLL_SECONDS)
            if event is not None:
                emitted_at, line = event
                last_output = emitted_at
                print(line, end="", flush=True)
        except queue.Empty:
            pass

        now = time.monotonic()
        if process.poll() is not None:
            reader.join(timeout=0.5)
            exit_code = int(process.returncode or 0)
            break
        if now - started >= timeout_seconds:
            outcome, exit_code = "wall-timeout", 124
            _terminate_process_tree(process, grace_seconds)
            break
        if now - last_output >= idle_timeout_seconds:
            outcome, exit_code = "idle-timeout", 125
            _terminate_process_tree(process, grace_seconds)
            break

    try:
        process.wait(timeout=max(1, grace_seconds + 1))
    except subprocess.TimeoutExpired:
        _terminate_process_tree(process, 0)
    finally:
        process.stdout.close()

    while True:
        try:
            event = events.get_nowait()
        except queue.Empty:
            break
        if event is not None:
            _, line = event
            print(line, end="", flush=True)

    duration = round(time.monotonic() - started, 3)
    result = GuardResult(
        normalized,
        working_directory,
        duration,
        int(exit_code if exit_code is not None else process.returncode or 0),
        outcome,
        timeout_seconds,
        idle_timeout_seconds,
    )
    if json_output:
        output_path = Path(json_output)
        output_path.parent.mkdir(parents=True, exist_ok=True)
        output_path.write_text(json.dumps(asdict(result), indent=2), encoding="utf-8")
    print(
        f"[guard] outcome={result.outcome} exit={result.exit_code} duration={duration:.3f}s",
        file=sys.stderr,
        flush=True,
    )
    return result


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--timeout-seconds", type=int, default=DEFAULT_TIMEOUT_SECONDS)
    parser.add_argument("--idle-timeout-seconds", type=int, default=DEFAULT_IDLE_TIMEOUT_SECONDS)
    parser.add_argument("--grace-seconds", type=int, default=DEFAULT_GRACE_SECONDS)
    parser.add_argument("--cwd")
    parser.add_argument("--json-output")
    parser.add_argument("command", nargs=argparse.REMAINDER)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    command = list(args.command)
    if command and command[0] == "--":
        command = command[1:]
    try:
        result = run_guarded(
            command,
            timeout_seconds=args.timeout_seconds,
            idle_timeout_seconds=args.idle_timeout_seconds,
            grace_seconds=args.grace_seconds,
            cwd=args.cwd,
            json_output=args.json_output,
        )
    except (OSError, ValueError) as exc:
        print(f"[guard] configuration error: {exc}", file=sys.stderr)
        return 64
    return result.exit_code


if __name__ == "__main__":
    raise SystemExit(main())
