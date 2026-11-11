# Trackdub 2 standards

Supplements `AGENTS.md`, which wins on conflict.

## .NET

- .NET 10 (`global.json`), nullable enabled, implicit usings.
- Prefer small sealed classes and records; static pure helpers for logic that tests can reach
  without network (signature checks, routing, prompt building, formatting).
- Internal helpers are tested through `InternalsVisibleTo`, not made public for tests.
- Inject `TimeProvider` for anything time-based; tests use `FakeTimeProvider`.
- Async all the way; pass `CancellationToken` through.
- String comparisons that matter for security or exact matching use `StringComparison.Ordinal`
  (culture-aware comparison ignores characters such as zero-width spaces).

## Security

- Entra ID through `DefaultAzureCredential`; no keys in code or config.
- Verify every inbound webhook before parsing it.
- Content from GitHub (PR text, diffs, logs) is untrusted: wrap it, defuse closing tags, and keep
  model output from mentioning users.
- Agents that read untrusted content get no tools unless the orchestrator approves it.

## Infrastructure

- Bicep under `infra/`; validate with build, lint and what-if before any deployment.
- One resource group for the spike; read per-phase spend by date range.
- Every billed service either has a job in the spike or is called out as a milestone workload.
