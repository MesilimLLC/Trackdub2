# Trackdub 2 spike infrastructure

Minimal Azure setup for the Trackdub 2 spike plan, in eastus.

## What it creates

| Resource | Purpose | Idle cost |
|---|---|---|
| Resource group `rg-trackdub2-spike` | One group for the whole spike | none |
| Foundry account (AIServices, S0) + project `trackdub2-spike` | gpt-6 deployments, Speech, evaluations, fine-tuning | none; billed per use |
| Deployments `gpt-6-luna` (50K TPM), `gpt-6-sol` (10K TPM) | Translator and meaning judge, GlobalStandard, pinned versions | none; billed per token |
| Storage account (Standard_LRS) + container `media` | Video translation input/output, clips | cents |
| Log Analytics (1 GB/day cap) + Application Insights | Agent Framework and Foundry tracing | cents |
| Role assignments | Foundry User on the account, Storage Blob Data Contributor on storage | none |
| Subscription budget `budget-trackdub2-spike` | Alerts at $100 / $500 / $600 / $2,100 / $4,000 of $5,000 | none |

Not created on purpose: VNets, firewall, gateway, AKS, App Service, SQL, Cosmos DB, Key Vault.
The pipeline runs as a CLI on your machine and signs in with Entra ID, so there are no keys to store.

## Before deploying

- The subscription is an Azure Sponsorship subscription (offer `Sponsored_2016-01-01`, checked
  2026-11-10), so eligible usage is paid from startup credits. Its spending limit is Off: anything
  the credits don't cover is charged to the card on file. The startup credit rules exclude
  third-party models, partner services and Marketplace purchases.
- The $5,000 credit tier is available for up to 180 days.
- Budget alerts lag spend by hours. They warn; they don't stop anything. The deployment
  capacities in `main.bicepparam` are the rate limit Azure enforces.

## Deploy (PowerShell)

```powershell
az bicep install
az provider register --namespace Microsoft.OperationalInsights
$env:AZURE_PRINCIPAL_ID = (az ad signed-in-user show --query id -o tsv)
az bicep build --file infra/main.bicep
az deployment sub what-if --location eastus --parameters infra/main.bicepparam
az deployment sub create --location eastus --name trackdub2-spike --parameters infra/main.bicepparam
```

The outputs include the project endpoint and deployment names the CLI needs.

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
