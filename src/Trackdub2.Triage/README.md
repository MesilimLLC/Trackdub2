# Trackdub2.Triage

Posts one triage comment on pull requests and failed CI runs, written by `gpt-6-luna` through a
Foundry project. Runs on App Service (`app-td2-triage-42dkfedvehtze`) as a GitHub App webhook.

## How it works

1. GitHub sends `pull_request` or `workflow_run` webhooks to `/github/webhook`.
2. The endpoint checks the `X-Hub-Signature-256` HMAC, keeps only allowed repositories, skips drafts
   and bot authors, queues a job and answers 202 within GitHub's 10-second limit.
3. The worker fetches the diff (or the failed jobs' log tails), wraps it in untrusted-data tags, and
   asks the agent for a comment. The agent has no tools, so injected text can only change that text.
4. The comment is created, or the app's earlier comment for the same PR or workflow is edited in place.
   @mentions in model output are neutralized so it can't notify anyone.
5. At most `MaxAgentCallsPerHour` agent calls run in any hour (default 30).

## Settings (App Service, from Bicep)

| Setting | Source |
|---|---|
| `Triage__ProjectEndpoint`, `Triage__ModelDeployment` | Foundry project and deployment |
| `Triage__GitHubAppId` | `GITHUB_APP_ID` at deploy time |
| `Triage__GitHubPrivateKey` | Key Vault secret `github-app-private-key` |
| `Triage__GitHubWebhookSecret` | Key Vault secret `github-webhook-secret` (already created) |
| `Triage__AllowedRepositories__N` | `triageAllowedRepositories` in `main.bicep` |

`GET /healthz` reports `githubConfigured`. It stays `false`, and webhooks get 503, until the app ID and
private key are in place.

## One-time GitHub App setup

These steps create a GitHub App on the MesilimLLC organization and need an org owner in a browser.

1. Open https://github.com/organizations/MesilimLLC/settings/apps/new and fill in:
   - **GitHub App name:** `Trackdub2 Triage` (names are global; add a suffix if it's taken)
   - **Homepage URL:** `https://github.com/MesilimLLC/Trackdub2`
   - **Webhook:** Active. **URL:** `https://app-td2-triage-42dkfedvehtze.azurewebsites.net/github/webhook`
   - **Webhook secret:** copy it from Key Vault without printing it:
     ```powershell
     az keyvault secret show --vault-name kv-td2-42dkfedvehtze --name github-webhook-secret --query value -o tsv | Set-Clipboard
     ```
   - **Repository permissions:** Pull requests: Read and write. Actions: Read-only. Metadata: Read-only.
     Leave everything else at No access.
   - **Subscribe to events:** Pull request, Workflow run.
   - **Where can this GitHub App be installed?** Only on this account.
2. Create the app. Note the **App ID** on its settings page.
3. Under **Private keys**, generate a key. Store the downloaded `.pem` in Key Vault, then delete the file:
   ```powershell
   az keyvault secret set --vault-name kv-td2-42dkfedvehtze --name github-app-private-key --file <path-to-key>.pem
   Remove-Item <path-to-key>.pem
   ```
4. Redeploy with the App ID so Bicep stays the source of truth, then restart so Key Vault references
   resolve:
   ```powershell
   $env:GITHUB_APP_ID = '<app id>'
   # plus AZURE_PRINCIPAL_ID / AZURE_PRINCIPAL_NAME as in infra/README.md
   az deployment sub create --location eastus --name trackdub2-spike --parameters infra/main.bicepparam
   az webapp restart -g rg-trackdub2-spike -n app-td2-triage-42dkfedvehtze
   ```
5. On the app's page, choose **Install App** > MesilimLLC > **Only select repositories** > Trackdub2.
6. Check `https://app-td2-triage-42dkfedvehtze.azurewebsites.net/healthz` shows `githubConfigured: true`,
   then open a test pull request.

## Deploying code

```powershell
dotnet publish src/Trackdub2.Triage -c Release -o publish
Compress-Archive publish/* triage.zip -Force
az webapp deploy -g rg-trackdub2-spike -n app-td2-triage-42dkfedvehtze --src-path triage.zip --type zip
```
