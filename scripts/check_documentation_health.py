#!/usr/bin/env python3
"""Validate and route durable backend documentation from DOCS_MANIFEST.json."""
from __future__ import annotations
import argparse, fnmatch, json, re, subprocess
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = {"path","title","class","owner","purpose","review_days","last_verified","source_globs","impact"}
IMPACTS = {"required","advisory"}
CONFLICT_RE = re.compile(r"^(?:<<<<<<< .+|=======|>>>>>>> .+)$", re.MULTILINE)
LINK_RE = re.compile(r"\[[^\]]+\]\(([^)]+)\)")

@dataclass(frozen=True)
class Finding:
    severity: str; path: Path; message: str
    def render(self, root: Path = ROOT) -> str:
        try: shown = self.path.relative_to(root)
        except ValueError: shown = self.path
        return f"[{self.severity}] {shown} - {self.message}"

def read(path: Path) -> str: return path.read_text(encoding="utf-8", errors="replace")
def norm(path: str) -> str:
    value = path.strip().replace("\\", "/")
    while value.startswith("./"): value = value[2:]
    return value

def matches(path: str, pattern: str) -> bool:
    path, pattern = norm(path), norm(pattern)
    return path.startswith(pattern[:-3]) if pattern.endswith("/**") else fnmatch.fnmatch(path, pattern)

def markdown_links(text: str) -> list[str]:
    kept, in_fence = [], False
    for line in text.splitlines():
        if line.strip().startswith("```"): in_fence = not in_fence; continue
        if not in_fence: kept.append(line)
    return LINK_RE.findall("\n".join(kept))

def resolve(source: Path, raw: str, root: Path) -> Path | None:
    target = raw.strip().strip("<>").split("#",1)[0]
    if not target or target.startswith(("http://","https://","mailto:","#")) or "<" in target or ">" in target: return None
    return root / target.lstrip("/") if target.startswith("/") else (source.parent / target).resolve()

def load(root: Path = ROOT) -> tuple[dict,list[Finding]]:
    path = root / "docs/DOCS_MANIFEST.json"
    if not path.exists(): return {}, [Finding("FAIL",path,"documentation manifest is missing")]
    try: return json.loads(read(path)), []
    except json.JSONDecodeError as exc: return {}, [Finding("FAIL",path,f"invalid JSON: {exc}")]

def docs(data: dict) -> list[dict]:
    value = data.get("documents")
    return value if isinstance(value,list) else []

def registry_text(items: Iterable[dict]) -> str:
    rows=[]
    for item in sorted(items,key=lambda x:x["path"].casefold()):
        link = item["path"].removeprefix("docs/") if item["path"].startswith("docs/") else "../"+item["path"]
        rows.append(f"| [`{item['path']}`]({link}) | {item['class']} | `{item['owner']}` | {item['impact']} | {item.get('last_verified') or 'unverified'} | {item['purpose']} |")
    return "# Backend Documentation Registry\n\nGenerated from [`DOCS_MANIFEST.json`](DOCS_MANIFEST.json). Do not edit by hand.\n\n| Document | Class | Owner | Impact | Last verified | Purpose |\n|---|---|---|---|---|---|\n"+"\n".join(rows)+"\n"

def validate(root: Path = ROOT, *, full_links: bool=False) -> list[Finding]:
    data, findings = load(root)
    if findings: return findings
    manifest, items = root/"docs/DOCS_MANIFEST.json", docs(data)
    if data.get("version") != 1: findings.append(Finding("FAIL",manifest,"manifest version must be 1"))
    if not items: return findings+[Finding("FAIL",manifest,"manifest documents must be a non-empty list")]
    seen, registered = set(), set()
    for i,item in enumerate(items):
        if not isinstance(item,dict): findings.append(Finding("FAIL",manifest,f"entry {i} must be an object")); continue
        missing=sorted(REQUIRED-set(item))
        if missing: findings.append(Finding("FAIL",manifest,f"entry {i} missing fields: {', '.join(missing)}")); continue
        rel=norm(str(item["path"])); target=root/rel
        if rel in seen: findings.append(Finding("FAIL",manifest,f"duplicate document path: {rel}"))
        seen.add(rel); registered.add(rel)
        if not target.exists(): findings.append(Finding("FAIL",target,"registered durable document is missing"))
        if item.get("impact") not in IMPACTS: findings.append(Finding("FAIL",manifest,f"{rel} impact must be required or advisory"))
        if not isinstance(item.get("review_days"),int) or item["review_days"]<=0: findings.append(Finding("FAIL",manifest,f"{rel} review_days must be positive"))
        globs=item.get("source_globs")
        if not isinstance(globs,list) or not globs or not all(isinstance(x,str) and x.strip() for x in globs): findings.append(Finding("FAIL",manifest,f"{rel} source_globs must be non-empty strings"))
        if target.exists() and target.suffix.lower()==".md":
            for match in CONFLICT_RE.finditer(read(target)): findings.append(Finding("FAIL",target,f"unresolved merge-conflict marker: {match.group(0).split()[0]}"))
    registry=root/"docs/DOCS_REGISTRY.md"; expected=registry_text(items)
    if not registry.exists(): findings.append(Finding("FAIL",registry,"generated registry is missing"))
    elif read(registry)!=expected: findings.append(Finding("FAIL",registry,"registry does not match DOCS_MANIFEST.json; regenerate it"))
    index=root/"docs/DOCS_INDEX.md"
    if index.exists():
        for raw in markdown_links(read(index)):
            target=resolve(index,raw,root)
            if target is None or target.suffix.lower()!=".md": continue
            try: rel=target.relative_to(root).as_posix()
            except ValueError: continue
            if rel not in registered: findings.append(Finding("FAIL",index,f"durable index links unregistered document: {rel}"))
    if full_links:
        for item in items:
            source=root/norm(str(item["path"]))
            if not source.exists() or source.suffix.lower()!=".md": continue
            for raw in markdown_links(read(source)):
                target=resolve(source,raw,root)
                if target is not None and not target.exists(): findings.append(Finding("FAIL",source,f"broken local link: {raw}"))
    return findings

def context_documents(paths: list[str], root: Path=ROOT) -> list[tuple[dict,list[str]]]:
    data, errors=load(root)
    if errors: raise ValueError(errors[0].message)
    result=[]
    for item in docs(data):
        hit=sorted({norm(path) for path in paths for pattern in item["source_globs"] if matches(path,pattern)})
        if hit: result.append((item,hit))
    return sorted(result,key=lambda x:(x[0]["impact"]!="required",x[0]["path"]))

def changed_paths(base: str, root: Path=ROOT) -> list[str]:
    result=subprocess.run(["git","diff","--diff-filter=AMDR","--name-only",f"{base}...HEAD"],cwd=root,text=True,capture_output=True,check=True,timeout=30)
    return [norm(x) for x in result.stdout.splitlines() if x.strip()]
def write_registry(root: Path=ROOT) -> None:
    data, errors=load(root)
    if errors: raise ValueError(errors[0].message)
    (root/"docs/DOCS_REGISTRY.md").write_text(registry_text(docs(data)),encoding="utf-8")
def main(argv: list[str]|None=None) -> int:
    parser=argparse.ArgumentParser(description=__doc__); parser.add_argument("--write-registry",action="store_true"); parser.add_argument("--full-links",action="store_true"); parser.add_argument("--context",nargs="+"); parser.add_argument("--changed-from"); args=parser.parse_args(argv)
    if args.write_registry: write_registry(ROOT)
    paths=args.context
    if args.changed_from:
        try: paths=changed_paths(args.changed_from,ROOT)
        except (OSError,subprocess.SubprocessError) as exc: print(f"[FAIL] unable to resolve changed paths: {exc}"); return 1
    if paths:
        print("Documentation context")
        for item,hit in context_documents(paths,ROOT): print(f"- {item['impact']}: {item['path']} <- {', '.join(hit[:8])}")
    findings=validate(ROOT,full_links=args.full_links); failures=[x for x in findings if x.severity=="FAIL"]
    print(f"Backend documentation health: documents={len(docs(load(ROOT)[0]))} failures={len(failures)}")
    for item in findings: print(item.render(ROOT))
    return 1 if failures else 0
if __name__=="__main__": raise SystemExit(main())
