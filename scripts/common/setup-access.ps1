param(
    [Parameter(Mandatory)]
    [string]$resourceGroup,

    [Parameter(Mandatory)]
    [string]$collaborationId,

    [Parameter(Mandatory)]
    [string]$subject,

    [Parameter(Mandatory)]
    [string]$issuerUrl,

    [string]$outDir = "./generated",

    [switch]$setupKeyVault = $false
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

Import-Module $PSScriptRoot/../azure-helpers/azure-helpers.psm1 -Force -DisableNameChecking
Import-Module $PSScriptRoot/common.psm1

Test-AzureAccessToken

$outDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($outDir)
$namesFile = Join-Path $outDir $resourceGroup "names.generated.ps1"
if (-not (Test-Path $namesFile)) {
    Write-Host "ERROR: '$namesFile' not found. Run 04-prepare-resources.ps1 first." -ForegroundColor Red
    exit 1
}
. $namesFile

# Support both naming styles from generated names files.
# Some environments emit UPPER_SNAKE_CASE variables.
if (-not $storageAccountId -and $STORAGE_ACCOUNT_ID) { $storageAccountId = $STORAGE_ACCOUNT_ID }
if (-not $keyVaultId -and $KEYVAULT_ID) { $keyVaultId = $KEYVAULT_ID }

if (-not $storageAccountId) {
    throw "storageAccountId is empty. Regenerate '$namesFile' by re-running scripts/04-prepare-resources.ps1."
}

if ($setupKeyVault -and -not $keyVaultId) {
    throw "keyVaultId is empty. Regenerate '$namesFile' by re-running scripts/04-prepare-resources.ps1."
}

$identityJson = az identity show --name $MANAGED_IDENTITY_NAME `
    --resource-group $resourceGroup --output json | ConvertFrom-Json

function Ensure-RoleAssignment {
    param(
        [Parameter(Mandatory)]
        [string]$role,

        [Parameter(Mandatory)]
        [string]$scope,

        [Parameter(Mandatory)]
        [string]$assigneeObjectId
    )

    $PSNativeCommandUseErrorActionPreference = $false
    $existing = & az role assignment list `
        --assignee-object-id $assigneeObjectId `
        --scope $scope `
        --fill-principal-name false `
        --fill-role-definition-name false `
        --role $role 2>$null
    $PSNativeCommandUseErrorActionPreference = $true

    $existingCount = 0
    if ($LASTEXITCODE -eq 0 -and $existing) {
        $existingCount = @($existing | ConvertFrom-Json).Count
    }

    if ($existingCount -gt 0) {
        Write-Log Warning `
            "Skipping assignment as '$role' permission already exists for '$($identityJson.name)' on '$scope'."
        return
    }

    Write-Log Verbose `
        "Assigning '$role' to '$($identityJson.name)' on '$scope'."
    az role assignment create `
        --role $role `
        --scope $scope `
        --assignee-object-id $assigneeObjectId `
        --assignee-principal-type ServicePrincipal
    CheckLastExitCode
}

Write-Host "=== Setting up collaborator access ===" -ForegroundColor Cyan
Write-Host "Managed identity: $MANAGED_IDENTITY_NAME" -ForegroundColor Yellow
Write-Host "Subject: $subject" -ForegroundColor Yellow
Write-Host "Issuer URL: $issuerUrl" -ForegroundColor Yellow

Ensure-RoleAssignment `
    -role "Storage Blob Data Contributor" `
    -scope $storageAccountId `
    -assigneeObjectId $identityJson.principalId

if ($setupKeyVault) {
    Ensure-RoleAssignment `
        -role "Key Vault Crypto Officer" `
        -scope $keyVaultId `
        -assigneeObjectId $identityJson.principalId

    Ensure-RoleAssignment `
        -role "Key Vault Secrets User" `
        -scope $keyVaultId `
        -assigneeObjectId $identityJson.principalId
}

$ficName = "$subject-federation"
$existingFic = az identity federated-credential list `
    --identity-name $MANAGED_IDENTITY_NAME `
    --resource-group $resourceGroup -o json | ConvertFrom-Json

if ($existingFic | Where-Object { $_.name -eq $ficName -and $_.issuer -eq $issuerUrl -and $_.subject -eq $subject }) {
    Write-Log Warning `
        "Federated credential '$ficName' already exists. Skipping creation."
}
else {
    Write-Log OperationStarted `
        "Creating federated credential '$ficName'..."
    az identity federated-credential create `
        --name $ficName `
        --identity-name $MANAGED_IDENTITY_NAME `
        --resource-group $resourceGroup `
        --issuer $issuerUrl `
        --subject $subject `
        --audiences api://AzureADTokenExchange
    CheckLastExitCode
    Write-Log OperationCompleted `
        "Created federated credential '$ficName'."
}

Write-Host "Access setup complete for '$subject'." -ForegroundColor Green