# Azure.Cleanroom.Analytics.Frontend.Client

C# client SDK for the **Analytics Frontend API** (`frontend.yaml`), generated using
[AutoRest](https://github.com/Azure/autorest) from the OpenAPI 3 specification at
`src/sdk/openapi/V2026_03_01_Preview/frontend.yaml`.

The project is packaged as a NuGet package: **`Azure.Cleanroom.Analytics.Frontend.Client`**.

---

## Prerequisites

- [Node.js](https://nodejs.org/) 18+ (for running AutoRest via `npx`)
- [.NET SDK 10.0](https://dotnet.microsoft.com/download)
- AutoRest CLI: `npm install -g autorest`

---

## Generating the client

From this folder, run:

```powershell
pwsh ./generate.ps1
```

This invokes AutoRest using the literate configuration declared below
(AutoRest reads its config from `README.md` by convention).

Generated sources land in `./generated/` and are compiled into the
`Azure.Cleanroom.Analytics.Frontend.Client` NuGet package.

---

## Building and packing the NuGet

```powershell
dotnet build  ./Azure.Cleanroom.Analytics.Frontend.Client.csproj -c Release
dotnet pack   ./Azure.Cleanroom.Analytics.Frontend.Client.csproj -c Release -o ./nupkg
```

The resulting `.nupkg` is written to `./nupkg/`.

---

## AutoRest configuration

> The remainder of this file is the literate AutoRest configuration consumed by
> the `autorest` CLI. Do not remove the code fences below.

``` yaml
# ----------------------------------------------------------------------------
# AutoRest configuration for the Analytics Frontend API C# SDK.
# ----------------------------------------------------------------------------

input-file:
  - ../../openapi/V2026_03_01_Preview/frontend.yaml

# OpenAPI 3 spec; AutoRest needs the openapi-to-typespec / modelerfour pipeline.
openapi-type: data-plane
modelerfour:
  lenient-model-deduplication: true
  flatten-models: true
  flatten-payloads: true

# Generator selection: official Azure C# generator (emits Azure.Core based code).
use:
  - "@autorest/csharp@3.0.0-beta.20251203.1"

csharp:
  namespace: Azure.Cleanroom.Analytics.Frontend.Client
  output-folder: ./auto-generated
  clear-output-folder: true
  public-clients: true
  head-as-boolean: false
  generation1-convenience-client: true
  client-side-validation: false
  sync-methods: all
  add-credentials: true
  license-header: MICROSOFT_MIT_NO_VERSION

# ----------------------------------------------------------------------------
# Spec workarounds.
# ----------------------------------------------------------------------------
# ApplicationState.state contains an empty-string enum entry which AutoRest
# cannot name; drop it so generation succeeds. The remaining named values
# (SUBMITTED, RUNNING, ...) are preserved.
directive:
  - from: openapi-document
    where: $.components.schemas.ApplicationState.properties.state
    transform: >
      $.enum = $.enum.filter(function (v) { return v !== ''; });
```

---

## Layout

```
src/sdk/clients/frontend-client/
├── README.md                                           # AutoRest config + docs
├── generate.ps1                                        # Generation entry point
├── Azure.Cleanroom.Analytics.Frontend.Client.csproj    # NuGet-packed project
└── generated/                                          # AutoRest output (do not edit)
```
