# Trackdub 2 architecture (current state)

## Built

| Component | Resource | Notes |
|---|---|---|
| Foundry account + project `trackdub2-spike` | `aif-td2-*` (eastus) | AIServices kind; also serves Speech |
| Models | `gpt-6-luna` 50K TPM, `gpt-6-sol` 10K TPM | GlobalStandard, pinned 2026-09-22 |
| Triage agent | App Service B1 `app-td2-triage-*` | GitHub App `trackdub2-triage`; webhook `/github/webhook`, health `/healthz` |
| Secrets | Key Vault `kv-td2-*` | `github-app-private-key`, `github-webhook-secret` |
| Run store | PostgreSQL 17 B1ms `psql-td2-*` (centralus) | Entra login only; database `trackdub2`; no schema yet |
| Images | Container Registry Basic `crtd2*` | Empty until the pipeline image exists |
| Media | Storage `sttd2*`, container `media` | Shared-key access off; user-delegation SAS |
| Telemetry | Log Analytics + Application Insights | 1 GB/day ingestion cap |
| Cost alerts | Subscription budget | $100 / $500 / $600 / $2,100 / $4,000 of $5,000 |

## Triage data flow

GitHub webhook → signature check → `WebhookRouter` (allowlisted repo, PR actions or failed run
with a PR) → bounded queue → worker → GitHub API (installation token) → `PromptBuilder` (untrusted
tags) → `gpt-6-luna` (no tools) → `CommentFormatter` → create or edit one comment.

## Planned, not built

- Dubbing pipeline CLI: fast transcription → translate/revise loop with timing gate → meaning gate
  (`gpt-6-sol`) → neural TTS → measured audio gate → mix. Gates ported from Trackdub Director.
- Shared Python `grade()` in `grader/`, used as Foundry custom evaluator and RFT grader; must
  match the C# timing gate on a fixed 10-sample check.
- Nightly regression job on Container Instances, writing results to PostgreSQL.
- GitHub Actions deploys through federated (OIDC) sign-in to Azure.
