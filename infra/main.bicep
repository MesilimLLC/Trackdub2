// Trackdub 2 Azure spike: subscription-scope entry point.
//
// Creates one resource group for the whole spike and a subscription budget whose alert
// thresholds match the spike plan's cumulative phase caps. All other resources live in
// resources.bicep at resource-group scope.
//
// One group, not one per phase: every phase shares the same Foundry account, so per-phase
// groups would not separate the spend. Phases run in sequence, so read phase spend in Cost
// Management by date range instead.
targetScope = 'subscription'

@description('Region for every resource. eastus has video translation, LLM speech, gpt-6 quota and o4-mini fine-tune quota on this subscription (checked 2026-11-10).')
param location string = 'eastus'

@description('Resource group for the whole spike.')
param resourceGroupName string = 'rg-trackdub2-spike'

@description('Entra object ID of the person running the spike. Receives Foundry User on the account and Storage Blob Data Contributor on the storage account.')
param principalId string

@description('Entra user principal name of the same person, used as the PostgreSQL Entra administrator. Read from the environment so it is not committed.')
param principalName string

@description('Public IP of the developer machine, allowed through the PostgreSQL firewall. Empty for no rule.')
param devClientIp string = ''

@description('Region for PostgreSQL. This subscription has no PostgreSQL Flexible Server offering in eastus (checked 2026-11-10); centralus is the nearest region that has it.')
param postgresLocation string = 'centralus'

@description('GitHub App ID for the triage agent. Empty until the app is created; the agent stays idle without it.')
param githubAppId string = ''

@description('Repositories the triage agent may comment on, as owner/name.')
param triageAllowedRepositories array = [
  'MesilimLLC/Trackdub2'
]

@description('App Service runtime for the triage agent.')
param triageAppRuntime string = 'DOTNETCORE|10.0'

@description('Type of principalId.')
@allowed([
  'User'
  'ServicePrincipal'
  'Group'
])
param principalType string = 'User'

@description('Entra ID only on the Foundry account. Set false only if a Speech API used in Phase 1 rejects Entra tokens; video translation Entra support has not been verified.')
param disableLocalAuth bool = true

// Capacity is in thousands of tokens per minute and is the fastest spend limit Azure
// enforces. Worst case per day, if every token were billed as output:
//   gpt-6-luna  50 -> 72M tokens/day x $0.50/1M  = about $36/day
//   gpt-6-sol   10 -> 14.4M tokens/day x $10/1M  = about $144/day
// Raise these only after the app's own call budget is in place.
@description('gpt-6-luna (translator) capacity, thousands of tokens per minute.')
@minValue(1)
param translatorCapacity int = 50

@description('gpt-6-sol (meaning judge) capacity, thousands of tokens per minute.')
@minValue(1)
param judgeCapacity int = 10

@description('Total startup credit tracked by the budget, in USD.')
param budgetAmount int = 5000

@description('First day of the month the budget starts (yyyy-MM-01). Pin it in the parameters file so redeploys do not move it.')
param budgetStartDate string = utcNow('yyyy-MM-01')

@description('Optional extra alert recipients. Subscription Owners are always notified, so no address has to be stored in the repo.')
param budgetContactEmails array = []

var tags = {
  project: 'trackdub2'
  purpose: 'spike'
}

// Cumulative spend at the end of each phase in the spike plan, plus a late warning.
// Budget alerts arrive hours after the spend because cost data lags, so they warn rather
// than stop. The hard stop is the app's own call budget (port Director's CallBudget) and
// the deployment capacities above.
var budgetAlerts = [
  { name: 'phase1Cap', usd: 100 }
  { name: 'phase2Cap', usd: 500 }
  { name: 'phase3Cap', usd: 600 }
  { name: 'phase4Cap', usd: 2100 }
  { name: 'creditWarning', usd: 4000 }
]

// 2025-04-01 is the newest version the Bicep CLI (0.49.2) has types for.
resource rg 'Microsoft.Resources/resourceGroups@2025-04-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  name: 'trackdub2-spike-resources'
  scope: rg
  params: {
    location: location
    tags: tags
    principalId: principalId
    principalName: principalName
    principalType: principalType
    disableLocalAuth: disableLocalAuth
    translatorCapacity: translatorCapacity
    judgeCapacity: judgeCapacity
    devClientIp: devClientIp
    triageAppRuntime: triageAppRuntime
    postgresLocation: postgresLocation
    githubAppId: githubAppId
    triageAllowedRepositories: triageAllowedRepositories
  }
}

resource budget 'Microsoft.Consumption/budgets@2024-08-01' = {
  name: 'budget-trackdub2-spike'
  properties: {
    category: 'Cost'
    amount: budgetAmount
    timeGrain: 'Annually'
    timePeriod: {
      startDate: budgetStartDate
    }
    notifications: toObject(budgetAlerts, alert => alert.name, alert => {
      enabled: true
      operator: 'GreaterThanOrEqualTo'
      threshold: (alert.usd * 100) / budgetAmount
      thresholdType: 'Actual'
      contactRoles: [
        'Owner'
      ]
      contactEmails: budgetContactEmails
    })
  }
}

output resourceGroupName string = rg.name
output foundryAccountName string = resources.outputs.foundryAccountName
output foundryEndpoint string = resources.outputs.foundryEndpoint
output projectEndpoint string = resources.outputs.projectEndpoint
output speechRegion string = location
output storageAccountName string = resources.outputs.storageAccountName
output mediaContainerName string = resources.outputs.mediaContainerName
output appInsightsName string = resources.outputs.appInsightsName
output translatorDeployment string = resources.outputs.translatorDeployment
output judgeDeployment string = resources.outputs.judgeDeployment
output postgresHost string = resources.outputs.postgresHost
output postgresDatabase string = resources.outputs.postgresDatabase
output registryLoginServer string = resources.outputs.registryLoginServer
output triageAppHostName string = resources.outputs.triageAppHostName
output keyVaultName string = resources.outputs.keyVaultName
