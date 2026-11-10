// Trackdub 2 spike resources, deployed at resource-group scope by main.bicep.
//
// - One Foundry (AIServices) account holds the gpt-6 deployments and Speech (fast
//   transcription, neural TTS, video translation), so there is no separate Speech resource.
// - Entra ID only by default: local keys are disabled on the account, shared-key access is off on
//   storage, PostgreSQL accepts Entra tokens only, and the registry has no admin user. Video
//   translation reads media through user-delegation SAS URLs, which work without account keys.
// - No VNets, firewall, gateway or Key Vault. The spike runs against public endpoints with Entra
//   auth; network isolation can come later if Trackdub 2 becomes a product.
// - PostgreSQL, Container Registry and App Service are real parts of the spike (run store, image
//   registry, triage agent host) and also count toward the Microsoft for Startups workload
//   milestone. Container Instances for the nightly job is added once the pipeline image exists.

param location string
param tags object
param principalId string
param principalName string
param principalType string
param disableLocalAuth bool
param translatorCapacity int
param judgeCapacity int
param devClientIp string
param triageAppRuntime string

var suffix = uniqueString(resourceGroup().id)
var accountName = 'aif-td2-${suffix}'
var storageName = 'sttd2${suffix}'
var projectName = 'trackdub2-spike'
var mediaContainerName = 'media'
var databaseName = 'trackdub2'
var projectEndpoint = 'https://${accountName}.services.ai.azure.com/api/projects/${projectName}'

// Built-in role definition IDs.
var foundryUserRoleId = '53ca6127-db72-4b80-b1b0-d745d6d5456d' // Foundry User (formerly Azure AI User)
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var acrPullRoleId = '7f951dda-4ed3-4680-a7ca-43fe172d538d'
var acrPushRoleId = '8311e382-0749-4cb8-b61a-304f252e45ec'

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

// ---------------------------------------------------------------------------------------------
// Observability

resource logs 'Microsoft.OperationalInsights/workspaces@2026-03-01' = {
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

// ---------------------------------------------------------------------------------------------
// Storage

resource storage 'Microsoft.Storage/storageAccounts@2026-09-01' = {
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

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2026-09-01' = {
  parent: storage
  name: 'default'
}

resource mediaContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2026-09-01' = {
  parent: blobService
  name: mediaContainerName
  properties: {
    publicAccess: 'None'
  }
}

// ---------------------------------------------------------------------------------------------
// Foundry: account, project, model deployments

resource account 'Microsoft.CognitiveServices/accounts@2026-09-01' = {
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

resource project 'Microsoft.CognitiveServices/accounts/projects@2026-09-01' = {
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
resource deployments 'Microsoft.CognitiveServices/accounts/deployments@2026-09-01' = [
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

// ---------------------------------------------------------------------------------------------
// PostgreSQL: run, verdict and corpus store

resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2026-10-01' = {
  name: 'psql-td2-${suffix}'
  location: location
  tags: tags
  sku: {
    name: 'Standard_B1ms'
    tier: 'Burstable'
  }
  properties: {
    version: '17'
    storage: {
      storageSizeGB: 32
      autoGrow: 'Disabled'
    }
    backup: {
      backupRetentionDays: 7
      geoRedundantBackup: 'Disabled'
    }
    highAvailability: {
      mode: 'Disabled'
    }
    // Entra tokens only, so no database password exists to leak.
    authConfig: {
      activeDirectoryAuth: 'Enabled'
      passwordAuth: 'Disabled'
      tenantId: tenant().tenantId
    }
    network: {
      publicNetworkAccess: 'Enabled'
    }
  }
}

// The server applies one change at a time, so the admin, database and firewall rules are chained.
resource postgresAdmin 'Microsoft.DBforPostgreSQL/flexibleServers/administrators@2026-10-01' = {
  parent: postgres
  name: principalId
  properties: {
    principalName: principalName
    principalType: principalType
    tenantId: tenant().tenantId
  }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2026-10-01' = {
  parent: postgres
  name: databaseName
  properties: {
    charset: 'UTF8'
    collation: 'en_US.utf8'
  }
  dependsOn: [
    postgresAdmin
  ]
}

// Lets Azure-hosted callers (the triage app, the nightly job) reach the server without a VNet.
// It admits any Azure service, so access rests on Entra auth, which is the only login method.
resource allowAzureServices 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2026-10-01' = {
  parent: postgres
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
  dependsOn: [
    database
  ]
}

resource allowDevClient 'Microsoft.DBforPostgreSQL/flexibleServers/firewallRules@2026-10-01' = if (!empty(devClientIp)) {
  parent: postgres
  name: 'AllowDevClient'
  properties: {
    startIpAddress: devClientIp
    endIpAddress: devClientIp
  }
  dependsOn: [
    allowAzureServices
  ]
}

// ---------------------------------------------------------------------------------------------
// Container Registry: pipeline and triage agent images

resource registry 'Microsoft.ContainerRegistry/registries@2025-11-01' = {
  name: 'crtd2${suffix}'
  location: location
  tags: tags
  sku: {
    name: 'Basic'
  }
  properties: {
    adminUserEnabled: false
    anonymousPullEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

// ---------------------------------------------------------------------------------------------
// App Service: triage agent host

resource plan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: 'asp-td2-${suffix}'
  location: location
  tags: tags
  kind: 'linux'
  sku: {
    name: 'B1'
    tier: 'Basic'
  }
  properties: {
    reserved: true
  }
}

resource triageApp 'Microsoft.Web/sites@2024-11-01' = {
  name: 'app-td2-triage-${suffix}'
  location: location
  tags: tags
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: triageAppRuntime
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      // Keeps the webhook endpoint warm so GitHub deliveries don't time out on a cold start.
      alwaysOn: true
      appSettings: [
        {
          name: 'FOUNDRY_PROJECT_ENDPOINT'
          value: projectEndpoint
        }
        {
          name: 'FOUNDRY_MODEL_DEPLOYMENT'
          value: modelDeployments[0].name
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
      ]
    }
  }
}

// Deployment goes through Entra-authenticated paths only; basic-auth FTP and SCM publishing are off.
resource triageFtpPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-11-01' = {
  parent: triageApp
  name: 'ftp'
  properties: {
    allow: false
  }
}

resource triageScmPolicy 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-11-01' = {
  parent: triageApp
  name: 'scm'
  properties: {
    allow: false
  }
}

// ---------------------------------------------------------------------------------------------
// Role assignments. Through Bicep these aren't automatic as they are in the portal.

resource userFoundryUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, principalId, foundryUserRoleId)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', foundryUserRoleId)
    principalId: principalId
    principalType: principalType
  }
}

// Includes the user-delegation key action needed to mint SAS URLs for video translation.
resource userBlobContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, principalId, storageBlobDataContributorRoleId)
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
    principalId: principalId
    principalType: principalType
  }
}

resource userAcrPush 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, principalId, acrPushRoleId)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPushRoleId)
    principalId: principalId
    principalType: principalType
  }
}

resource triageFoundryUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(account.id, triageApp.id, foundryUserRoleId)
  scope: account
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', foundryUserRoleId)
    principalId: triageApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource triageAcrPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(registry.id, triageApp.id, acrPullRoleId)
  scope: registry
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', acrPullRoleId)
    principalId: triageApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output foundryAccountName string = account.name
output foundryEndpoint string = account.properties.endpoint
output projectEndpoint string = projectEndpoint
output storageAccountName string = storage.name
output mediaContainerName string = mediaContainer.name
output appInsightsName string = appInsights.name
output translatorDeployment string = deployments[0].name
output judgeDeployment string = deployments[1].name
output postgresHost string = postgres.properties.fullyQualifiedDomainName
output postgresDatabase string = database.name
output registryLoginServer string = registry.properties.loginServer
output triageAppHostName string = triageApp.properties.defaultHostName
