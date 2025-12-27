# Consul Configuration Loader for TradingService
# This script loads configuration from appsettings.json and .env to Consul KV store

param(
    [string]$ConsulHost = "http://localhost:8500",
    [string]$Environment = "Production",
    [string]$EnvFile = ".env",
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "=== Consul Configuration Loader ===" -ForegroundColor Cyan
Write-Host "Consul Host: $ConsulHost" -ForegroundColor Gray
Write-Host "Environment: $Environment" -ForegroundColor Gray
Write-Host "Env File: $EnvFile" -ForegroundColor Gray
Write-Host ""

# Function to load .env file
function Load-EnvFile {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        Write-Warning "Env file not found: $Path"
        return @{}
    }

    $envVars = @{}
    Get-Content $Path | ForEach-Object {
        $line = $_.Trim()
        # Skip comments and empty lines
        if ($line -and -not $line.StartsWith("#")) {
            $parts = $line -split "=", 2
            if ($parts.Length -eq 2) {
                $key = $parts[0].Trim()
                $value = $parts[1].Trim()
                $envVars[$key] = $value
            }
        }
    }

    return $envVars
}

# Function to set Consul key
function Set-ConsulKey {
    param(
        [string]$Key,
        [string]$Value,
        [string]$ConsulUrl
    )

    $fullKey = "TradingService/$Environment/$Key"
    $url = "$ConsulUrl/v1/kv/$fullKey"

    if ($DryRun) {
        Write-Host "[DRY-RUN] Would set: $fullKey = $Value" -ForegroundColor Yellow
        return
    }

    try {
        $body = [System.Text.Encoding]::UTF8.GetBytes($Value)
        Invoke-RestMethod -Uri $url -Method Put -Body $body -ContentType "application/json" | Out-Null
        Write-Host "[OK] Set: $fullKey" -ForegroundColor Green
    }
    catch {
        Write-Error "Failed to set ${fullKey}: ${_}"
    }
}

# Load environment variables from .env
$scriptDir = Split-Path -Parent $PSScriptRoot
$envFilePath = Join-Path $scriptDir $EnvFile

Write-Host "Loading environment variables from: $envFilePath" -ForegroundColor Cyan
$envVars = Load-EnvFile -Path $envFilePath

if ($envVars.Count -eq 0) {
    Write-Warning "No environment variables loaded from $EnvFile"
}
else {
    Write-Host "Loaded $($envVars.Count) environment variables" -ForegroundColor Green
}

# Load appsettings.json
$appsettingsPath = Join-Path $scriptDir "src\TradingService\appsettings.json"
Write-Host "Loading configuration from: $appsettingsPath" -ForegroundColor Cyan

if (-not (Test-Path $appsettingsPath)) {
    Write-Error "appsettings.json not found at: $appsettingsPath"
    exit 1
}

$appSettings = Get-Content $appsettingsPath | ConvertFrom-Json

# Test Consul connectivity
Write-Host "Testing Consul connectivity..." -ForegroundColor Cyan
try {
    $consulStatus = Invoke-RestMethod -Uri "$ConsulHost/v1/status/leader" -Method Get
    Write-Host "Consul is reachable" -ForegroundColor Green
}
catch {
    Write-Error "Cannot reach Consul at $ConsulHost. Make sure Consul is running."
    exit 1
}

Write-Host ""
Write-Host "=== Uploading Configuration to Consul ===" -ForegroundColor Cyan
Write-Host ""

# Upload non-sensitive settings from appsettings.json
Write-Host "--- Non-Sensitive Settings ---" -ForegroundColor Yellow

Set-ConsulKey -Key "AppSettings:ScanTime" -Value $appSettings.AppSettings.ScanTime -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:Watchlist" -Value ($appSettings.AppSettings.Watchlist -join ",") -ConsulUrl $ConsulHost

# Strategy settings
Set-ConsulKey -Key "AppSettings:Strategy:MinExpiryDays" -Value $appSettings.AppSettings.Strategy.MinExpiryDays -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:Strategy:MaxExpiryDays" -Value $appSettings.AppSettings.Strategy.MaxExpiryDays -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:Strategy:MinConfidence" -Value $appSettings.AppSettings.Strategy.MinConfidence -ConsulUrl $ConsulHost

# Consul settings
Set-ConsulKey -Key "AppSettings:Consul:Host" -Value $appSettings.AppSettings.Consul.Host -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:Consul:ServiceName" -Value $appSettings.AppSettings.Consul.ServiceName -ConsulUrl $ConsulHost

# Broker non-sensitive settings
Set-ConsulKey -Key "AppSettings:Broker:DefaultBroker" -Value $appSettings.AppSettings.Broker.DefaultBroker -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:Broker:Exante:Environment" -Value $appSettings.AppSettings.Broker.Exante.Environment -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:Broker:Exante:BaseUrl" -Value $appSettings.AppSettings.Broker.Exante.BaseUrl -ConsulUrl $ConsulHost

# Options Discovery settings
Set-ConsulKey -Key "AppSettings:OptionsDiscovery:Enabled" -Value $appSettings.AppSettings.OptionsDiscovery.Enabled -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:OptionsDiscovery:MinOpenInterest" -Value $appSettings.AppSettings.OptionsDiscovery.MinOpenInterest -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:OptionsDiscovery:MinVolume" -Value $appSettings.AppSettings.OptionsDiscovery.MinVolume -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:OptionsDiscovery:SampleOptionsPerUnderlying" -Value $appSettings.AppSettings.OptionsDiscovery.SampleOptionsPerUnderlying -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:OptionsDiscovery:FallbackToWatchlist" -Value $appSettings.AppSettings.OptionsDiscovery.FallbackToWatchlist -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:OptionsDiscovery:IncludeCallOptions" -Value $appSettings.AppSettings.OptionsDiscovery.IncludeCallOptions -ConsulUrl $ConsulHost
Set-ConsulKey -Key "AppSettings:OptionsDiscovery:MaxExpiryDays" -Value $appSettings.AppSettings.OptionsDiscovery.MaxExpiryDays -ConsulUrl $ConsulHost

Write-Host ""
Write-Host "--- Sensitive Settings (from .env) ---" -ForegroundColor Yellow

# Upload sensitive settings from .env
if ($envVars.ContainsKey("EXANTE_API_KEY")) {
    $maskedKey = $envVars["EXANTE_API_KEY"].Substring(0, [Math]::Min(8, $envVars["EXANTE_API_KEY"].Length)) + "***"
    Write-Host "Uploading EXANTE_API_KEY ($maskedKey)..." -ForegroundColor Gray
    Set-ConsulKey -Key "AppSettings:Broker:Exante:ApiKey" -Value $envVars["EXANTE_API_KEY"] -ConsulUrl $ConsulHost
}

if ($envVars.ContainsKey("EXANTE_API_SECRET")) {
    Write-Host "Uploading EXANTE_API_SECRET (***hidden***)..." -ForegroundColor Gray
    Set-ConsulKey -Key "AppSettings:Broker:Exante:ApiSecret" -Value $envVars["EXANTE_API_SECRET"] -ConsulUrl $ConsulHost
}

if ($envVars.ContainsKey("EXANTE_ACCOUNT_ID")) {
    $maskedAccount = $envVars["EXANTE_ACCOUNT_ID"].Substring(0, [Math]::Min(8, $envVars["EXANTE_ACCOUNT_ID"].Length)) + "***"
    Write-Host "Uploading EXANTE_ACCOUNT_ID ($maskedAccount)..." -ForegroundColor Gray
    Set-ConsulKey -Key "AppSettings:Broker:Exante:AccountId" -Value $envVars["EXANTE_ACCOUNT_ID"] -ConsulUrl $ConsulHost
}

if ($envVars.ContainsKey("EXANTE_JWT_TOKEN")) {
    Write-Host "Uploading EXANTE_JWT_TOKEN (***hidden***)..." -ForegroundColor Gray
    Set-ConsulKey -Key "AppSettings:Broker:Exante:JwtToken" -Value $envVars["EXANTE_JWT_TOKEN"] -ConsulUrl $ConsulHost
}

if ($envVars.ContainsKey("DATABASE_CONNECTION_STRING")) {
    Write-Host "Uploading DATABASE_CONNECTION_STRING..." -ForegroundColor Gray
    Set-ConsulKey -Key "AppSettings:Database:ConnectionString" -Value $envVars["DATABASE_CONNECTION_STRING"] -ConsulUrl $ConsulHost
}

Write-Host ""
Write-Host "=== Configuration Upload Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "You can view the configuration in Consul UI:" -ForegroundColor Cyan
Write-Host "$ConsulHost/ui/dc1/kv/TradingService/$Environment/" -ForegroundColor White
Write-Host ""

if ($DryRun) {
    Write-Host "This was a DRY-RUN. No changes were made to Consul." -ForegroundColor Yellow
}
