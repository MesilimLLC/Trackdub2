---
description: "Run the CI-equivalent gate: restore, Release build, tests, Bicep build and lint"
agent: subagents/validation-gate
---

Run every gate in order and report PASS / FAIL / NOT VERIFIED for each, with evidence.
Modify nothing. Finish with one line: READY or NOT READY, and the first failing gate if any.
