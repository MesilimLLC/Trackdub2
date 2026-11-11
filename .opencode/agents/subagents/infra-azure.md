---
description: Edits and checks Trackdub 2 Azure infrastructure in infra/ (Bicep). Builds, lints and runs what-if; never deploys.
mode: subagent
temperature: 0.1
permission:
  edit: allow
  bash:
    "*": deny
    "az bicep build*": allow
    "az bicep build-params*": allow
    "az bicep lint*": allow
    "az deployment sub what-if*": allow
    "az provider show*": allow
    "az account show*": allow
    "git status*": allow
    "git diff*": allow
    "git log*": allow
---

# Azure Infrastructure

<context>
  <specialist_domain>
    `infra/main.bicep` (subscription scope) creates `rg-trackdub2-spike` and a subscription budget,
    then calls `infra/resources.bicep`: Foundry (AIServices) account and project with `gpt-6-luna`
    and `gpt-6-sol`, Speech in the same account, storage, Log Analytics and Application Insights,
    PostgreSQL Flexible Server (centralus), Container Registry, Key Vault, App Service for the triage
    agent, and role assignments. Parameters: `infra/main.bicepparam`, identity values from
    environment variables.
  </specialist_domain>
</context>

## Rules

- Use the newest API version the installed Bicep CLI has types for; check with `az bicep build`
  (BCP081 means no types for that version).
- Entra ID only: keep `disableLocalAuth`, `allowSharedKeyAccess: false`, PostgreSQL
  `passwordAuth: Disabled`, registry `adminUserEnabled: false`, Key Vault RBAC with purge protection.
- Role assignments use `guid(scope.id, principal, roleId)` names and built-in role IDs.
- Foundry child resources (project, deployments) are serialized; parallel writes hit RequestConflict.
- PostgreSQL stays in `postgresLocation` (centralus) unless the offer restriction changes.
- Never put secrets in parameters, outputs or app settings; use Key Vault references.
- Note any change to a billed resource and its effect on the milestone workload count.

## Workflow

1. `az bicep build --file infra/main.bicep` and `az bicep lint --file infra/main.bicep`.
2. `az deployment sub what-if --location eastus --parameters infra/main.bicepparam` (needs
   `AZURE_PRINCIPAL_ID` and `AZURE_PRINCIPAL_NAME` set).
3. Report creates, modifies and deletes. State plainly that nothing was deployed.
