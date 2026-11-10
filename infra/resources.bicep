// Trackdub 2 spike resources, deployed at resource-group scope by main.bicep.
//
// - One Foundry (AIServices) account holds the gpt-6 deployments and Speech (fast
//   transcription, neural TTS, video translation), so there is no separate Speech resource.
// - Entra ID only by default: local keys are disabled on the account and shared-key access is
//   off on storage. The CLI signs in with DefaultAzureCredential. Video translation reads media
//   through user-delegation SAS URLs, which work without account keys.
// - No networking, compute, Key Vault or databases. The pipeline runs as a CLI on the
//   developer's machine against public endpoints, as the spike plan describes.

param location string
param tags object
param principalId string
param principalType string
param disableLocalAuth bool
param translatorCapacity int
param judgeCapacity int

var suffix = uniqueString(resourceGroup().id)
var accountName = 'aif-td2-${suffix}'
var storageName = 'sttd2${suffix}'
var projectName = 'trackdub2-spike'
var mediaContainerName = 'media'

// Built-in role definition IDs.
var foundryUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d' // Foundry User (formerly Azure AI User)
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

// Model versions are pinned with auto-upgrade off: a silent model change mid-spike would
// invalidate comparisons between runs.
var modelDeployments = [
  {
    name: 'gpt-6-luna'
    model: 'gpt-6-luna'
    version: '2026-09-22'
    capacity: translatorCapacity
  }
  {
    name: 'gpt-6-sol'
    model: 'gpt-6-sol'
    version: '2026-09-22'
    capacity: judgeCapacity
  }
]

resource logs 'Microsoft.OperationalInsights/workspaces@2025-02-01' = {
  name: 'log-td2-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
    // Caps ingestion spend if tracing is left on in a loop; spike telemetry is far below 1 GB/day.
    workspaceCapping: {
      dailyQuotaGb: 1
    }
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-td2-${suffix}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logs.id
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2025-01-01' = {
  name: storageName
  location: location
  tags: tags
  kind: 'StorageV2'
  // Spike media can be regenerated from the source clips, so locally redundant storage is enough.
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    publicNetworkAccess: 'Enabled'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2025-01-01' = {
  parent: storage
  name: 'default'
}

resource mediaContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2025-01-01' = {
  parent: blobService
  name: mediaContainerName
  properties: {
    publicAccess: 'None'
  }
}

resource account 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: accountName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: {
    name: 'S0'
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    // A custom subdomain is required for Entra tokens and for Foundry projects.
    customSubDomainName: accountName
    allowProjectManagement: true
    disableLocalAuth: disableLocalAuth
    publicNetworkAccess: 'Enabled'
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: account
  name: projectName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    displayName: 'Trackdub 2 spike'
    description: 'Agent-driven dubbing spike: gated translation, evaluation and fine-tuning.'
  }
}

// One account accepts only one deployment change at a time, so create them serially.
@batchSize(1)
resource deployments 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = [
  for d in modelDeployments: {
    parent: account
    name: d.name
    sku: {
      name: 'GlobalStandard'
      capacity: d.capacity
    }
    properties: {
      model: {
        format: 'OpenAI'
        name: d.model
        version: d.version
      }
      versionUpgradeOption: 'NoAutoUpgrade'
    }
  }
]

// Role assignments made through the portal happen automatically; through Bicep they don't,
// so the developer gets them explicitly here.
resource foundryUserAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, principalId, foundryUserRoleId)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', foundryUserRoleId)
    principalId: principalId
    principalType: principalType
  }
}

// Includes the user-delegation key action needed to mint SAS URLs for video translation.
resource blobContributorAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, principalId, storageBlobDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: principalId
    principalType: principalType
  }
}

output foundryAccountName string = account.name
output foundryEndpoint string = account.properties.endpoint
output projectEndpoint string = 'https://${accountName}.services.ai.azure.com/api/projects/${project.name}'
output storageAccountName string = storage.name
output mediaContainerName string = mediaContainer.name
output appInsightsName string = appInsights.name
output translatorDeployment string = deployments[0].name
output judgeDeployment string = deployments[1].name
