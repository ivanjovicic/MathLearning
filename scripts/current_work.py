#!/usr/bin/env python3
"""Read-only CURRENT_WORK snapshot; derived from canonical owners and never a claim."""
from __future__ import annotations
import argparse, datetime as dt, json, re, subprocess
from pathlib import Path

B = chr(96)
TASK = re.compile(r"^##\s+((?:BCI|STAB|RQ|QDB|MT|GAI|DEX|RL|DT|PERF|OBS|SEC|Q)\d+[A-Z]?|P-UI-\d+)\b([^\n]*)", re.M)
ID = re.compile(r"(?:(?:BCI|STAB|RQ|QDB|MT|GAI|DEX|RL|DT|PERF|OBS|SEC|Q)\d+[A-Z]?|P-UI-\d+|(?:BACKEND|BE|MOB|TOOL|UI|FLUTTER)[-A-Z0-9]+)")
LIVE = {"READY", "IN_PROGRESS", "WAITING", "BLOCKED", "PARTIAL"}

def read(root, path):
    p = root / path
    return p.read_text(encoding="utf-8-sig") if p.is_file() else ""

def run_git(root, *args):
    try:
        p = subprocess.run(["git", *args], cwd=root, capture_output=True, text=True, timeout=6)
        return p.stdout.strip() if p.returncode == 0 else None
    except (OSError, subprocess.TimeoutExpired):
        return None

def clean(value):
    return re.sub(r"\s+", " ", value.replace(B, "").replace("|", "/")).strip()

def state(value):
    s = clean(value).upper().replace(" ", "_")
    for prefix, result in [("DONE","DONE"),("COMPLETED","DONE"),("ARCHIVED","OBSOLETE"),
                           ("SUPERSEDED","OBSOLETE"),("IN_PROGRESS","IN_PROGRESS"),("READY","READY"),
                           ("WAITING","WAITING"),("NEEDS_","WAITING"),("BLOCKED","BLOCKED"),("PARTIAL","PARTIAL")]:
        if s.startswith(prefix):
            return result
    return "UNKNOWN"

def field(text, name):
    match = re.search(r"^" + re.escape(name) + r":\s*(.+)$", text, re.I | re.M)
    return clean(match.group(1)) if match else ""

def row(task_id, status, priority, path, name="", owner="", blocker="", checked=""):
    return dict(id=task_id, status=status, priority=priority or "unknown", path=path,
                name=name, owner=owner or "unknown", blocker=blocker or "not specified",
                checked=checked or "not recorded")

def trendplus(root):
    folder = root / "docs/ai"
    fixed = ["BACKEND_CI_REPAIR_PROMPT_QUEUE.md", "BACKEND_CI_REPAIR_EVIDENCE_ADDENDUM.md",
             "STABILIZATION_RELEASE_SECURITY_PROMPT_QUEUE.md", "DATA_SOURCE_CONNECTOR_PROMPT_QUEUE.md",
             "MULTITENANCY_PROMPT_QUEUE.md", "GENAI_PRODUCT_PROMPT_QUEUE.md",
             "ANALYTICS_UI_PREMIUM_PROMPT_QUEUE.md", "SQL_ANALYTICS_PROMPT_QUEUE.md",
             "DECISION_INTELLIGENCE_PROMPT_QUEUE.md", "PLATFORM_EVOLUTION_PROMPT_QUEUE.md"]
    paths = [folder / n for n in fixed] + sorted(folder.glob("ANALYTICS_RELIABILITY_PROMPT_QUEUE*.md"))
    tasks, warnings = [], []
    for path in paths:
        if not path.is_file():
            warnings.append("Missing owning queue: " + str(path.relative_to(root)))
            continue
        s = path.read_text(encoding="utf-8-sig")
        matches = list(TASK.finditer(s))
        for i, m in enumerate(matches):
            part = s[m.end():matches[i+1].start() if i+1 < len(matches) else len(s)]
            front = part.split("\n### ", 1)[0]
            status = state(field(front, "Status"))
            if status in LIVE:
                tasks.append(row(m[1], status, field(front,"Priority"),path.relative_to(root).as_posix(),
                                 clean(m[2]).strip(" -"),field(front,"Owner"),
                                 field(front,"Ready after") or field(front,"Dependencies"),
                                 field(front,"Last checked")))
    master = read(root, "MASTER_ROADMAP.md")
    routing = re.search(r"^## LIVE Program routing matrix\b([\s\S]*?)(?=^## |\Z)",master,re.M)
    programs, first = [], {}
    if routing:
        for line in routing[1].splitlines():
            cells = [v.strip() for v in line.strip("|").split("|")] if line.startswith("|") else []
            if len(cells) >= 3 and cells[0] not in {"Program", "---"}:
                programs.append(cells[0])
                found = ID.search(cells[2])
                if found:
                    first[cells[0]] = found[0]
    else:
        warnings.append("LIVE routing matrix missing; priority not certified")
    families = ("BCI","STAB","P-UI","RQ","QDB","MT","GAI","DEX","RL","DT","PERF","OBS","SEC","Q")
    def family(task):
        return next((v for v in families if task["id"].startswith(v)), "Q")
    def sortkey(t):
        p = re.search(r"\bP([0-3])\b",t["priority"],re.I)
        f = family(t)
        return (programs.index(f) if f in programs else 999, 0 if first.get(f)==t["id"] else 1,
                int(p[1]) if p else 9, t["id"])
    tasks.sort(key=sortkey)
    return tasks,warnings,2,"[Trendplus master roadmap](MASTER_ROADMAP.md)"

def active_tables(root, paths):
    tasks, errors = [], []
    for name in paths:
        s = read(root,name)
        if not s:
            errors.append("Missing owning queue: " + name)
            continue
        region = re.search(r"^## Active prompts\b([\s\S]*?)(?=^## |\Z)",s,re.M|re.I)
        if not region: continue
        for line in region[1].splitlines():
            if not line.startswith("|"): continue
            cols = [v.strip() for v in line.strip("|").split("|")]
            if len(cols)<5: continue
            key = clean(cols[0])
            if not re.fullmatch(r"[A-Za-z][A-Za-z0-9_-]*\d+[A-Za-z0-9_-]*",key): continue
            status = state(cols[2])
            if status in LIVE:
                tasks.append(row(key,status,clean(cols[1]),name,clean(cols[4])[:120],
                                 "not established by queue row","see exact canonical prompt"))
    return tasks,errors

def backend(root):
    router=read(root,"docs/prompt_queues/README.md")
    section=re.search(r"^## Current canonical priority\b([\s\S]*?)(?=^## |\Z)",router,re.M)
    paths=[]
    if section:
        paths=list(dict.fromkeys("docs/prompt_queues/"+x for x in re.findall(r"backend_[\w]+\.md",section[1])))
    tasks,warnings=active_tables(root,paths)
    if not paths: warnings.append("Missing canonical priority router; priorities unverified")
    return tasks,warnings,None,"[Backend canonical priority](docs/prompt_queues/README.md)"

def flutter(root):
    router=read(root,"docs/prompt_queues/README.md")
    section=re.search(r"^## Active queue index\b([\s\S]*?)(?=^## |\Z)",router,re.M)
    paths=[]
    if section:
        paths=list(dict.fromkeys("docs/prompt_queues/"+x for x in
              re.findall(r"\[[^\]]+\]\(([^)#]+\.md)\)",section[1]) if not x.startswith("../")))
    tasks,warnings=active_tables(root,paths)
    if not paths: warnings.append("Missing canonical active queue index")
    warnings.append("Remote claim/lease NOT checked; run canonical python scripts/prompt_agent.py status --all-claims --json before selecting work")
    return tasks,warnings,None,"[Flutter prompt lifecycle](docs/prompt_queues/PROMPT_LIFECYCLE.md)"

def evidence(path, sha):
    if not path: return "NOT VERIFIED (no exact-SHA validation evidence)"
    try:
        data=json.loads(path.read_text(encoding="utf-8"))
        check=str(data["sha"]); command=str(data["command"]); result=str(data["result"]).lower()
        when=dt.datetime.fromisoformat(str(data["time"]).replace("Z","+00:00"))
        if not re.fullmatch(r"[0-9a-f]{40}",check) or not when.tzinfo or not command or result not in {"passed","failed","skipped"}:
            raise ValueError("invalid evidence")
        if check != sha: return "STALE: evidence for "+check[:12]+" differs from inspected HEAD; NOT GREEN"
        return result.upper()+" on "+check[:12]+" / "+command+" / "+when.isoformat()+" (provided evidence, not independent CI certification)"
    except (OSError,KeyError,ValueError,TypeError):
        return "INVALID evidence; NOT GREEN"

def render(root, validation=None, now=None):
    root=Path(root)
    if (root/"MASTER_ROADMAP.md").is_file():
        tasks,warnings,cap,objective=trendplus(root)
    elif (root/"scripts/prompt_agent.py").is_file():
        tasks,warnings,cap,objective=flutter(root)
    elif (root/"docs/prompt_queues/README.md").is_file():
        tasks,warnings,cap,objective=backend(root)
    else: raise ValueError("Cannot identify canonical repository")
    head=run_git(root,"rev-parse","HEAD")
    origin=run_git(root,"rev-parse","refs/remotes/origin/main")
    branch=run_git(root,"symbolic-ref","--short","HEAD")
    verified=bool(head and head==origin and branch=="main")
    duplicates={}
    for t in tasks: duplicates.setdefault(t["id"],[]).append(t)
    conflicts={k for k,v in duplicates.items() if len({(x["status"],x["path"]) for x in v})>1}
    if conflicts: warnings.append("Conflicting nonterminal owners (NO CLAIM): "+", ".join(sorted(conflicts)[:20]))
    active=[t for t in tasks if t["status"]=="IN_PROGRESS"]
    busy=len({t["id"] for t in active})
    ready=[t for t in tasks if t["status"]=="READY" and t["id"] not in conflicts]
    blocked=[t for t in tasks if t["status"] in {"BLOCKED","WAITING","PARTIAL"}]
    if cap is not None and busy>cap: warnings.append("WIP overflow; stop and reconcile ownership")
    def link(t): return "["+t["id"]+"]("+t["path"]+")"
    def table(header,rows):
        if not rows: return "_No verified rows._"
        return "| "+" | ".join(header)+" |\n|"+ "|".join("---" for _ in header)+"|\n"+\
               "\n".join("| "+" | ".join(str(x).replace("|","/") for x in r)+" |" for r in rows)
    now=now or dt.datetime.now(dt.timezone.utc)
    lines=["# CURRENT_WORK (generated, NEVER manually edited)", "",
           "Updated UTC: "+now.astimezone(dt.timezone.utc).isoformat(),
           "HEAD: "+str(head or "unknown")+"; main: "+("tracking origin/main matched" if verified else "UNVERIFIED; refresh and confirm remotely"),
           "","## Direction",objective,"",
           "## Active and WIP",
           (str(busy)+"/"+str(cap)+" recorded IN_PROGRESS; nominal free "+str(max(0,cap-busy))+"; NOT CONFIRMED FREE" if cap is not None
            else "UNKNOWN WIP: no repository-wide claim authority established by this offline view"),
           table(["ID / owner queue","Owner (source-derived)","Status","Last checked"],
                 [[link(t),t["owner"],t["status"],t["checked"]] for t in active[:20]]),
           "No time limit (including 24 hours) automatically releases claims.","",
           "## READY (informational ONLY; dependencies and claims require canonical selection)",
           table(["ID / queue","Priority","Scope"],[[link(t),t["priority"],t["name"]] for t in ready[:20]]),
           "Showing "+str(min(20,len(ready)))+" of "+str(len(ready))+" READY rows; order follows canonical router/priority and never authorizes claiming.","",
           "## Blocked / partial",
           table(["ID / queue","Status","Condition","Last verified"],
                 [[link(t),t["status"],t["blocker"],t["checked"]] for t in blocked[:20]]),
           "Missing last-check date or vague condition means UNVERIFIED, not automatically unblocked.","",
           "## SHA and validation",
           "Inspected HEAD: "+str(head or "unknown")+"; "+("local main tracking ref matches" if verified else "remote main unverified"),
           "Validation: "+evidence(validation,head)+(" — NOT GREEN FOR MAIN (branch/remote main unverified)" if not verified else ""),"",
           "## Integrity warnings",
           "\n".join("- "+x for x in warnings) if warnings else "- None detected within this limited source projection.",
           "","## Source-of-truth and lifecycle",
           "Queue files and remote claim protocol own status and owner; roadmap/router owns priorities; run logs are historical.",
           "This file is generated and overwritten, never committed, and is not a second owner/queue.",""]
    return "\n".join(lines)

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--root",type=Path,default=Path(__file__).resolve().parents[1])
    ap.add_argument("--output",type=Path)
    ap.add_argument("--validation-json",type=Path)
    args=ap.parse_args()
    try:
        result=render(args.root.resolve(),args.validation_json)
        if args.output:
            target=args.output if args.output.is_absolute() else args.root/args.output
            target.write_text(result,encoding="utf-8")
            print("Generated "+str(target))
        else: print(result)
    except (OSError,ValueError) as exc:
        print("CURRENT_WORK failed closed: "+str(exc))
        return 2
    return 0

if __name__=="__main__": raise SystemExit(main())
