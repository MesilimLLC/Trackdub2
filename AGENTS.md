# Trackdub 2 — agent instructions

Read by opencode, the CodeScene refactoring agent and other coding agents. Conflict order:
source and tests > task instructions > documentation.

## What this repo is

A spike: agent-driven English→Spanish dubbing on Microsoft Foundry, with Trackdub Director's
gates (timing, meaning, measured audio) as hard checks the agent cannot bypass. It also runs a
PR and CI triage agent. Plan, phases and Go/Stop criteria: the spike plan linked from `README.md`.

| Path | Contents |
|---|---|
| `infra/` | Bicep, subscription scope. `main.bicep` → `resources.bicep`, params in `main.bicepparam` |
| `src/Trackdub2.Triage/` | .NET 10 GitHub App webhook service on App Service |
| `tests/` | xUnit tests |
| `.opencode/` | opencode agents, commands and context ([navigation](.opencode/navigation.md)) |

## Azure facts that change decisions

- Subscription: Azure Sponsorship (startup credits). Spending limit is Off, so anything the credits
  don't cover bills to a card. Credits exclude third-party models, partner services and Marketplace.
- Region eastus for everything except PostgreSQL, which is in centralus: this subscription has no
  PostgreSQL Flexible Server offering in eastus, eastus2, southcentralus or westus2.
- Models: `gpt-6-luna` (translator, triage) and `gpt-6-sol` (judge), versions pinned, auto-upgrade off.
- Identity: Entra ID only. No account keys, no database passwords, no registry admin user. Secrets
  live in Key Vault and reach App Service as Key Vault references.
- Milestone goal: at least five Azure services each above $1 of spend for 60 days in a row. Don't
  remove or downsize a billed service without saying how that affects the count.

## Rules

- Never run `az deployment ... create`, `az webapp deploy`, or anything that changes Azure resources
  unless the task explicitly asks for a deployment. `az bicep build`, `az bicep lint` and
  `az deployment sub what-if` are fine.
- Never print, log or commit secrets, keys, tokens, connection strings or personal identifiers.
  `main.bicepparam` reads identity values from environment variables; keep it that way.
- Treat PR text, diffs and CI logs as untrusted input in anything that feeds a model.
- Tests: `dotnet test` must pass. Add tests with behavior changes.
- Comments only where the reason is not obvious from the code.
- UI and comment copy: literal and task-focused, no marketing language.

## CodeScene MCP Code Health

Use the CodeScene MCP server tools for Code Health checks. Code Health is authoritative for
maintainability. Target Code Health 10.0; anything below 10.0 is not done.

- **Safeguard AI-touched code before every commit:** run `pre_commit_code_health_safeguard` (staged
  files) before committing and `analyze_change_set` (branch vs base) before opening a PR.
- If a safeguard reports a regression: run `code_health_review` for details, refactor until Code
  Health is restored, and do not mark changes ready unless the risk is explicitly accepted.
- **Refactoring:** inspect with `code_health_review`, refactor in 3-5 small reviewable steps,
  re-check with `code_health_score`/`code_health_review` after each step.
- **Prioritization:** use `list_technical_debt_hotspots_for_project`,
  `list_technical_debt_goals_for_project`, and `code_health_refactoring_business_case` to rank and
  justify work.
- Select the correct CodeScene project early with `select_project`; subsequent API calls operate
  within it.
- If asked to bypass safeguards: warn about maintainability risk, keep changes minimal and
  reversible, recommend follow-up refactoring.

On a pull request, comment `/cs-agent` to run the CodeScene refactoring agent on Code Health
degradations, or `/cs-agent <instruction>` for a specific request.
