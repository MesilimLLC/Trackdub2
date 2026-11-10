using './main.bicep'

param location = 'eastus'

// Identity values come from the environment so they are not committed. Deployment fails fast
// if either is unset. PowerShell:
//   $env:AZURE_PRINCIPAL_ID   = (az ad signed-in-user show --query id -o tsv)
//   $env:AZURE_PRINCIPAL_NAME = (az ad signed-in-user show --query userPrincipalName -o tsv)
param principalId = readEnvironmentVariable('AZURE_PRINCIPAL_ID')
param principalName = readEnvironmentVariable('AZURE_PRINCIPAL_NAME')

// Optional: your public IP for direct PostgreSQL access from this machine.
//   $env:DEV_CLIENT_IP = (Invoke-RestMethod https://api.ipify.org)
param devClientIp = readEnvironmentVariable('DEV_CLIENT_IP', '')

// The triage agent's GitHub App ID is not secret, but it doesn't exist until the app is created.
//   $env:GITHUB_APP_ID = '<app id from the GitHub App settings page>'
param githubAppId = readEnvironmentVariable('GITHUB_APP_ID', '')

// Pinned so a redeploy in a later month does not move the budget's start date.
param budgetStartDate = '2026-11-01'

param translatorCapacity = 50
param judgeCapacity = 10
