# Trackdub 2 spike infrastructure

Azure setup for the Trackdub 2 spike plan, in eastus. It also covers the Microsoft for Startups
Milestone 3 goal: at least five Azure services, each above $1 of spend, for 60 days in a row.

## What it creates

| Resource | Purpose | Approx. monthly | Milestone workload |
|---|---|---|---|
| Foundry account (AIServices, S0) + project `trackdub2-spike` | gpt-6 deployments, evaluations, fine-tuning | per use | Foundry Models |
| (same account) | Speech: transcription, TTS, video translation | per use | Azure Speech |
| Deployments `gpt-6-luna` (50K TPM), `gpt-6-sol` (10K TPM) | Translator and meaning judge; versions pinned | per token | (Foundry Models) |
| PostgreSQL Flexible Server B1ms, Postgres 17, database `trackdub2` | Runs, verdicts, corpus; Entra login only | ~$12 + storage | PostgreSQL |
| Container Registry Basic | Pipeline and triage agent images; no admin user | ~$5 | Container Registry |
| App Service B1 Linux + web app `app-td2-triage-*` | Triage agent host; managed identity, basic-auth publishing off | ~$13 | App Service |
| Storage account (Standard_LRS) + container `media` | Video translation input/output, clips | cents | buffer, probably under $1 |
| Log Analytics (1 GB/day cap) + Application Insights | Tracing | likely free tier | no |
| Role assignments | You: Foundry User, Blob Data Contributor, AcrPush. Triage app: Foundry User, AcrPull | none | |
| Subscription budget `budget-trackdub2-spike` | Alerts at $100 / $500 / $600 / $2,100 / $4,000 of $5,000 | none | |

Not here yet: Container Instances for the nightly regression job. It's added once the pipeline
image exists, so nothing runs a placeholder container. Not created on purpose: VNets, firewall,
gateway, AKS, SQL, Cosmos DB, Key Vault (Key Vault comes with the triage agent's GitHub secrets).

## Before deploying

- The subscription is an Azure Sponsorship subscription (offer `Sponsored_2016-01-01`, checked
  2026-11-10), so eligible usage is paid from startup credits. Its spending limit is Off: anything
  the credits don't cover is charged to the card on file. The startup credit rules exclude
  third-party models, partner services and Marketplace purchases.
- The $5,000 tier lasts up to 180 days from business verification, and Milestone 3 must be met
  inside that window.
- Budget alerts lag spend by hours. They warn; they don't stop anything. The deployment
  capacities in `main.bicepparam` are the rate limit Azure enforces.

## Deploy (PowerShell)

```powershell
az provider register --namespace Microsoft.OperationalInsights
$env:AZURE_PRINCIPAL_ID   = (az ad signed-in-user show --query id -o tsv)
$env:AZURE_PRINCIPAL_NAME = (az ad signed-in-user show --query userPrincipalName -o tsv)
$env:DEV_CLIENT_IP        = (Invoke-RestMethod https://api.ipify.org)   # optional
az bicep build --file infra/main.bicep
az deployment sub what-if --location eastus --parameters infra/main.bicepparam
az deployment sub create --location eastus --name trackdub2-spike --parameters infra/main.bicepparam
```

The outputs include the project endpoint, deployment names, PostgreSQL host, registry login server
and triage app host name.

What-if passed on 2026-11-10 (25 creates, no changes or deletes). What-if doesn't check model
quota, the Postgres major version or name availability; those surface on the first real deploy.

## If a Speech API rejects Entra tokens

Set `disableLocalAuth` to `false` in `main.bicepparam` and redeploy. Video translation's Entra
support has not been verified yet; Phase 1 finds out.

## Tear down

```powershell
az group delete --name rg-trackdub2-spike
az consumption budget delete --budget-name budget-trackdub2-spike
```

The budget is at subscription scope, so deleting the group leaves it in place. A deleted Foundry
account is soft-deleted; purge it with `az cognitiveservices account purge` before reusing its name.
