. $PSScriptRoot/write-log.ps1

function Initialize-AppAuth {
    param(
        [Parameter(Mandatory)]
        [string]$appId,

        [Parameter(Mandatory)]
        [string]$tenantId,

        [Parameter(Mandatory)]
        [string]$certPemPath
    )

    if (-not (Test-Path $certPemPath)) {
        throw "Certificate file '$certPemPath' not found."
    }

    Write-Log OperationStarted `
        "Signing in as service principal '$appId' in tenant '$tenantId'..."

    az login `
        --service-principal `
        --username $appId `
        --tenant $tenantId `
        --password $certPemPath `
        --output none

    if ($LASTEXITCODE -ne 0) {
        throw "Azure CLI service principal login failed."
    }

    Write-Log OperationCompleted `
        "Signed in as service principal '$appId'."
}

function Get-CurrentPrincipalObjectId {
    $accountInfo = (az account show --query user -o json) | ConvertFrom-Json

    if ($accountInfo.type -eq "servicePrincipal") {
        return (az ad sp show --id $accountInfo.name --query id -o tsv)
    }

    return (az ad signed-in-user show --query id -o tsv)
}