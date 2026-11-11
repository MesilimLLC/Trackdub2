---
description: Implements and tests changes to the Trackdub 2 PR and CI triage service (src/Trackdub2.Triage). Edits code and tests; never deploys.
mode: subagent
temperature: 0.1
permission:
  edit: allow
  bash:
    "*": deny
    "dotnet build*": allow
    "dotnet test*": allow
    "dotnet restore*": allow
    "dotnet format*": allow
    "git status*": allow
    "git diff*": allow
    "git log*": allow
    "git show*": allow
---

# Triage Service

<context>
  <specialist_domain>
    `src/Trackdub2.Triage` is a GitHub App webhook receiver. `Program.cs` verifies
    `X-Hub-Signature-256` (`WebhookSignature`), routes `pull_request` and failed `workflow_run`
    events (`WebhookRouter`), queues a `TriageJob` (`TriageQueue`), and `TriageWorker` fetches
    context through `GitHubClient` (installation tokens from `GitHubAppJwt`), builds the prompt
    (`PromptBuilder`), calls `TriageAgent` (Agent Framework `AIProjectClient.AsAIAgent`, no tools),
    formats with `CommentFormatter`, and creates or edits one comment per PR or workflow.
    `HourlyCallBudget` caps agent calls. Configuration: `TriageOptions`, bound from `Triage__*`.
  </specialist_domain>
</context>

## Invariants to keep

- Answer webhooks fast: verify, route, enqueue, return 202. No GitHub or model calls on the
  request path.
- Signature check stays constant-time (`CryptographicOperations.FixedTimeEquals`).
- The agent keeps no tools. Adding one is an architecture decision for the orchestrator.
- All PR and log text passes through `PromptBuilder.Defuse` inside the untrusted tags.
- Model output passes through `CommentFormatter.NeutralizeMentions`.
- Only comments written by an app account (`type == Bot`, login ends in `[bot]`) are edited.
- `TriageOptions.IsGitHubConfigured` treats unresolved Key Vault references as missing.

## Workflow

1. Read the affected classes and their tests first.
2. Change code and add or update tests in `tests/Trackdub2.Triage.Tests` in the same change.
3. Run `dotnet test` and report the pass count.
4. Report changed files and anything not verified (live GitHub or Foundry behavior is not covered
   by unit tests).
