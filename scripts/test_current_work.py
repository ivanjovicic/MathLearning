"""Focused, offline tests for safe CURRENT_WORK generation."""
import datetime as dt
import json
import sys
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
sys.path.insert(0,str(Path(__file__).parent))
import current_work as c

class TestCurrentWork(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root=Path(self.tmp.name)
    def put(self,p,text):
        file=self.root/p
        file.parent.mkdir(parents=True,exist_ok=True)
        file.write_text(text,encoding="utf-8")
    def out(self):
        with patch.object(c,"run_git",return_value=None):
            return c.render(self.root,now=dt.datetime(2026,10,20,tzinfo=dt.timezone.utc))
    def test_trendplus_24h_is_not_owner_release(self):
        self.put("MASTER_ROADMAP.md","## LIVE Program routing matrix\n| Program | Queue | Current READY |\n|---|---|---|\n| RQ | q | RQ12 |\n")
        self.put("docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md","## RQ12 - Work\nStatus: IN_PROGRESS\nOwner: agent\nLast checked: 2026-09-01\n")
        result=self.out()
        self.assertIn("1/2 recorded IN_PROGRESS",result)
        self.assertIn("2026-09-01",result)
        self.assertIn("automatically releases",result)
    def test_duplicate_claim_fails_closed(self):
        self.put("MASTER_ROADMAP.md","## LIVE Program routing matrix\n| Program | Queue | Current READY |\n|---|---|---|\n| RQ | q | RQ12 |\n")
        self.put("docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE.md","## RQ12 - Task\nStatus: IN_PROGRESS\n")
        self.put("docs/ai/ANALYTICS_RELIABILITY_PROMPT_QUEUE_POST_IMPORT_VALUE_ADDENDUM.md","## RQ12 - Task\nStatus: READY\n")
        self.assertIn("Conflicting nonterminal owners",self.out())
    def test_backend_uses_priority_router_only(self):
        self.put("docs/prompt_queues/README.md","## Current canonical priority\n| 1 | backend_active.md | x |\n")
        self.put("docs/prompt_queues/backend_active.md","## Active prompts\n| ID | Priority | Status | Prompt | Purpose |\n|---|---|---|---|---|\n| BACKEND-API-001 | P1 | Ready | link | Fix auth |\n| BACKEND-API-002 | P1 | Done 95% | link | Historical |\n")
        result=self.out()
        self.assertIn("BACKEND-API-001",result)
        self.assertNotIn("BACKEND-API-002",result)
    def test_flutter_remote_claim_never_inferred(self):
        self.put("scripts/prompt_agent.py","# owned selector\n")
        self.put("docs/prompt_queues/README.md","## Active queue index\n- [Active](active.md)\n\n## Historical\n- [Old](old.md)\n")
        self.put("docs/prompt_queues/active.md","## Active prompts\n| ID | Priority | Status | Prompt | Purpose |\n|---|---|---|---|---|\n| MOB-321 | P0 | Ready | link | Fix quiz |\n")
        result=self.out()
        self.assertIn("Remote claim/lease NOT checked",result)
        self.assertNotIn("Old",result)
    def test_evidence_exact_sha(self):
        evidence=self.root/"evidence.json"
        evidence.write_text(json.dumps({"sha":"a"*40,"command":"python -m unittest","result":"passed","time":"2026-10-20T14:00:00Z"}))
        self.assertIn("PASSED",c.evidence(evidence,"a"*40))
        self.assertIn("STALE",c.evidence(evidence,"b"*40))
        evidence.write_text(json.dumps({"sha":"a"*40,"command":"test","result":"passed","time":"2026-10-20T14:00:00"}))
        self.assertIn("INVALID",c.evidence(evidence,"a"*40))
if __name__=="__main__": unittest.main()
