param(
    [Parameter(Mandatory)]
    [string]$resourceGroup,

    [string]$location = "westus",

    [string]$outDir = "./generated",

    [ValidateSet("blob")]
    [string]$storageType = "blob",

    [string]$appId,

    [string]$appTenantId,

    [string]$appCertPemPath
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true

. "$PSScriptRoot/setup-local-auth.ps1"
Import-Module $PSScriptRoot/common.psm1
Import-Module $PSScriptRoot/../azure-helpers/azure-helpers.psm1 -Force -DisableNameChecking

if ($appId -and $appCertPemPath -and $appTenantId) {
    Initialize-AppAuth -appId $appId -tenantId $appTenantId -certPemPath $appCertPemPath
}

Test-AzureAccessToken

Write-Host "Preparing resources for analytics scenario..." -ForegroundColor Cyan
Write-Host "Storage type: $storageType" -ForegroundColor Yellow

az group create --location $location --name $resourceGroup --output none
CheckLastExitCode

az provider register -n 'Microsoft.Storage' | Out-Null
az provider register -n 'Microsoft.KeyVault' | Out-Null
az provider register -n 'Microsoft.ManagedIdentity' | Out-Null

$uniqueString = Get-UniqueString($resourceGroup)
$managedIdentityName = "${uniqueString}-mi"
$storageAccountName = "datasa${uniqueString}"
$keyVaultName = "${uniqueString}kv"

$callerObjectId = Get-CurrentPrincipalObjectId

Write-Host "Creating managed identity '$managedIdentityName'..." -ForegroundColor Cyan
az identity create --name $managedIdentityName --resource-group $resourceGroup --location $location --output none
CheckLastExitCode

Write-Host "Creating storage account '$storageAccountName'..." -ForegroundColor Cyan
$storageResult = Create-Storage-Resources `
    -resourceGroup $resourceGroup `
    -storageAccountNames @($storageAccountName) `
    -objectId $callerObjectId
$storageAccountId = @($storageResult)[0].id

Write-Host "Creating Key Vault '$keyVaultName'..." -ForegroundColor Cyan
$keyVaultResult = (az keyvault create `
    --resource-group $resourceGroup `
    --name $keyVaultName `
    --sku premium `
    --enable-rbac-authorization true `
    --enable-purge-protection true) | ConvertFrom-Json

if (-not $keyVaultResult) {
    $keyVaultResult = (az keyvault show --name $keyVaultName --resource-group $resourceGroup) | ConvertFrom-Json
}
$keyVaultId = $keyVaultResult.id

az role assignment create `
    --role "Key Vault Crypto Officer" `
    --scope $keyVaultId `
    --assignee-object-id $callerObjectId `
    --assignee-principal-type $(Get-Assignee-Principal-Type) | Out-Null
CheckLastExitCode

az role assignment create `
    --role "Key Vault Secrets Officer" `
    --scope $keyVaultId `
    --assignee-object-id $callerObjectId `
    --assignee-principal-type $(Get-Assignee-Principal-Type) | Out-Null
CheckLastExitCode

$resourceDir = Join-Path $outDir $resourceGroup
New-Item -ItemType Directory -Path $resourceDir -Force | Out-Null

$namesFile = Join-Path $resourceDir "names.generated.ps1"
@'
$RESOURCE_GROUP = "{0}"
$RESOURCE_GROUP_LOCATION = "{1}"
$MANAGED_IDENTITY_NAME = "{2}"
$STORAGE_ACCOUNT_NAME = "{3}"
$KEYVAULT_NAME = "{4}"
$STORAGE_ACCOUNT_ID = "{5}"
$KEYVAULT_ID = "{6}"
'@ -f $resourceGroup, $location, $managedIdentityName, $storageAccountName, $keyVaultName, $storageAccountId, $keyVaultId | Set-Content -Path $namesFile -Encoding utf8

Write-Host "Resource names written to '$namesFile'." -ForegroundColor Green

return @{
    resourceGroup = $resourceGroup
    location = $location
    managedIdentityName = $managedIdentityName
    storageAccountName = $storageAccountName
    keyVaultName = $keyVaultName
    storageAccountId = $storageAccountId
    keyVaultId = $keyVaultId
}