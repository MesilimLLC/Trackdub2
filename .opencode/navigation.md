# Trackdub 2 — `.opencode` system index

Scope: `MesilimLLC/Trackdub2` only. Trackdub (desktop core), Trackdub-gated and Trackdub-Director
are separate repositories with their own agent setups.

## Quick start

```
/validate          # tests, Release build, Bicep build and lint. Read-only.
/whatif            # Bicep what-if against the subscription. Read-only preview, never deploys.
```

Run `/validate` before declaring any change done.

## Agents

| Agent | Mode | Owns |
|---|---|---|
| `trackdub2-orchestrator` | primary | Triage, routing, architecture and docs |
| `subagents/triage-service` | subagent | `src/Trackdub2.Triage`, its tests |
| `subagents/infra-azure` | subagent | `infra/` Bicep; build, lint, what-if only |
| `subagents/validation-gate` | subagent | Read-only gate: tests, build, Bicep checks |

## Context

- `context/architecture.md` — resources, regions, identity, data flow, what is not built yet.
- `context/standards.md` — code, security and infrastructure rules beyond `AGENTS.md`.

Load context on demand; the orchestrator keeps its own prompt lean.

## MCP servers (`opencode.json`)

- `microsoft-learn`: Microsoft and Azure documentation; no credentials.
- `trackdub-docs-rag`: Trackdub documentation. Needs `DOCS_RAG_TOKEN` in the environment, so it
  works in local runs only; the GitHub workflows don't pass that secret.
