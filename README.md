# Trackdub 2

Agent-driven English→Spanish dubbing on Microsoft Foundry. An orchestrator agent plans each line
and chooses repairs; deterministic gates carried over from Trackdub Director (timing, meaning,
measured audio) verify every result, and the agent has no path around them.

This repository is a spike. It answers one question: does the gated agent pipeline produce a
better-timed dub than Azure's built-in video translation, at a cost the startup credits can carry?

- Spike plan: https://claude.ai/artifact/1DRauDP7JAp22FXosSEfmS (private)
- Infrastructure: [`infra/`](infra/README.md), Bicep for eastus

## Layout

| Path | Contents |
|---|---|
| `infra/` | Bicep: Foundry account and models, Speech, storage, tracing, budget |
| `src/Trackdub2.Triage/` | PR and CI triage agent on App Service ([setup](src/Trackdub2.Triage/README.md)) |
| `tests/` | Unit tests |
| `src/` (more) | Pipeline CLI and gates ported from Director (to come) |
| `grader/` | Shared Python `grade()` used as Foundry evaluator and RFT grader (to come) |
| `.github/workflows/ci.yml` | Tests and Bicep build on every PR and push to main |
| `.github/workflows/opencode*.yml` | OpenCode PR review, and the OpenCode bot on comments |
| `.github/workflows/refactoring-agent.yml` | CodeScene refactoring agent, triggered by `/cs-agent` on a PR |
| `AGENTS.md`, `.opencode/` | Agent instructions, opencode agents and commands |

Trackdub (desktop) and Trackdub Director are separate repositories. This one copies ideas and
gate code from Director; it doesn't depend on either at runtime.
