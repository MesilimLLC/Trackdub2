using './main.bicep'

param location = 'eastus'

// Read from the environment so no object ID is committed. Deployment fails fast if unset.
//   PowerShell: $env:AZURE_PRINCIPAL_ID = (az ad signed-in-user show --query id -o tsv)
param principalId = readEnvironmentVariable('AZURE_PRINCIPAL_ID')

// Pinned so a redeploy in a later month does not move the budget's start date.
param budgetStartDate = '2026-11-01'

param translatorCapacity = 50
param judgeCapacity = 10
