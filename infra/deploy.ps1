# Foundry Demo - Infrastructure Deployment Script
# This script deploys the Foundry Demo infrastructure to Azure

param(
    [Parameter(Mandatory=$false)]
    [string]$Environment = "dev",
    
    [Parameter(Mandatory=$false)]
    [string]$ResourceGroupName = "rg-foundry-demo-$Environment",
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "swedencentral",
    
    [Parameter(Mandatory=$false)]
    [string]$SubscriptionId,
    
    [Parameter(Mandatory=$false)]
    [switch]$WhatIf
)

# Set error action preference
$ErrorActionPreference = "Stop"

Write-Host "🚀 Starting Foundry Demo Infrastructure Deployment" -ForegroundColor Green
Write-Host "Environment: $Environment" -ForegroundColor Yellow
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Yellow
Write-Host "Location: $Location" -ForegroundColor Yellow

# Check if Azure CLI is installed
try {
    $azVersion = az version --output json | ConvertFrom-Json
    Write-Host "✅ Azure CLI version: $($azVersion.'azure-cli')" -ForegroundColor Green
}
catch {
    Write-Error "❌ Azure CLI is not installed or not accessible. Please install Azure CLI first."
    exit 1
}

# Login check
try {
    $account = az account show --output json | ConvertFrom-Json
    Write-Host "✅ Logged in as: $($account.user.name)" -ForegroundColor Green
    
    if ($SubscriptionId -and $account.id -ne $SubscriptionId) {
        Write-Host "🔄 Setting subscription to: $SubscriptionId" -ForegroundColor Yellow
        az account set --subscription $SubscriptionId
    }
}
catch {
    Write-Error "❌ Not logged in to Azure. Please run 'az login' first."
    exit 1
}

# Create resource group if it doesn't exist
Write-Host "🔍 Checking if resource group exists..." -ForegroundColor Blue
$rgExists = az group exists --name $ResourceGroupName --output tsv
if ($rgExists -eq "false") {
    Write-Host "📁 Creating resource group: $ResourceGroupName" -ForegroundColor Yellow
    az group create --name $ResourceGroupName --location $Location --output none
} else {
    Write-Host "✅ Resource group already exists: $ResourceGroupName" -ForegroundColor Green
}

# Determine which Bicep file to use
$bicepFile = if ($Environment -eq "dev") { "main-dev.bicep" } else { "main.bicep" }
$parametersFile = "parameters.json"

if (!(Test-Path $bicepFile)) {
    Write-Error "❌ Bicep file not found: $bicepFile"
    exit 1
}

if (!(Test-Path $parametersFile)) {
    Write-Error "❌ Parameters file not found: $parametersFile"
    exit 1
}

# Deploy infrastructure
$deploymentName = "foundry-demo-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
Write-Host "🏗️  Starting deployment: $deploymentName" -ForegroundColor Blue

try {
    if ($WhatIf) {
        Write-Host "🔍 Running deployment validation (What-If)..." -ForegroundColor Yellow
        az deployment group what-if `
            --resource-group $ResourceGroupName `
            --template-file $bicepFile `
            --parameters $parametersFile `
            --name $deploymentName
    } else {
        Write-Host "🚀 Deploying infrastructure..." -ForegroundColor Blue
        $deploymentResult = az deployment group create `
            --resource-group $ResourceGroupName `
            --template-file $bicepFile `
            --parameters $parametersFile `
            --name $deploymentName `
            --output json | ConvertFrom-Json
            
        if ($deploymentResult.properties.provisioningState -eq "Succeeded") {
            Write-Host "✅ Deployment completed successfully!" -ForegroundColor Green
            
            # Display outputs
            if ($deploymentResult.properties.outputs) {
                Write-Host "`n📋 Deployment Outputs:" -ForegroundColor Blue
                $deploymentResult.properties.outputs | ConvertTo-Json -Depth 3 | Write-Host
            }
        } else {
            Write-Error "❌ Deployment failed with state: $($deploymentResult.properties.provisioningState)"
        }
    }
}
catch {
    Write-Error "❌ Deployment failed: $($_.Exception.Message)"
    exit 1
}

if (!$WhatIf) {
    Write-Host "`n🎉 Foundry Demo infrastructure deployment completed!" -ForegroundColor Green
    Write-Host "📌 Next steps:" -ForegroundColor Yellow
    Write-Host "   1. Configure AI Foundry connector in Logic Apps" -ForegroundColor White
    Write-Host "   2. Set up Office 365 connection for email Logic App" -ForegroundColor White
    Write-Host "   3. Deploy application code to Function App and Container App" -ForegroundColor White
    Write-Host "   4. Configure Key Vault secrets for production" -ForegroundColor White
}