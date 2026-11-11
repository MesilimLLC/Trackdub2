---
description: "Preview Azure changes with Bicep what-if. Never deploys."
agent: subagents/infra-azure
---

Build and lint `infra/main.bicep`, then run:

```
az deployment sub what-if --location eastus --parameters infra/main.bicepparam
```

If `AZURE_PRINCIPAL_ID` or `AZURE_PRINCIPAL_NAME` is unset, stop and say which one. Report the
resources that would be created, modified or deleted, and flag any delete or any change to a billed
resource. End with: "Nothing was deployed."
