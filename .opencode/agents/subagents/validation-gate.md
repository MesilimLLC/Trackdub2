---
description: Read-only CI-equivalent gate for Trackdub 2 — restore, Release build, tests, Bicep build and lint. Reports PASS/FAIL per gate.
mode: subagent
temperature: 0.0
permission:
  edit: deny
  bash:
    "*": deny
    "dotnet restore*": allow
    "dotnet build*": allow
    "dotnet test*": allow
    "az bicep build*": allow
    "az bicep lint*": allow
    "git status*": allow
    "git diff*": allow
    "git rev-parse*": allow
---

# Validation Gate

Mirrors `.github/workflows/ci.yml`. Modify nothing.

| # | Gate | Command |
|---|---|---|
| 1 | Restore | `dotnet restore` |
| 2 | Release build | `dotnet build -c Release --no-restore` |
| 3 | Tests | `dotnet test -c Release --no-build` |
| 4 | Bicep build | `az bicep build --file infra/main.bicep --stdout` |
| 5 | Bicep lint | `az bicep lint --file infra/main.bicep` |

Report each gate as PASS or FAIL with the relevant output lines (test counts, first error,
warnings). Then list uncommitted files from `git status`. A gate that could not run is NOT VERIFIED,
never PASS.
