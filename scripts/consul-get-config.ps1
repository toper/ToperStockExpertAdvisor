# Consul Configuration Viewer for TradingService
# This script displays configuration stored in Consul KV store

param(
    [string]$ConsulHost = "http://localhost:8500",
    [string]$Environment = "Production",
    [switch]$ShowSensitive
)

$ErrorActionPreference = "Stop"

Write-Host "=== Consul Configuration Viewer ===" -ForegroundColor Cyan
Write-Host "Consul Host: $ConsulHost" -ForegroundColor Gray
Write-Host "Environment: $Environment" -ForegroundColor Gray
Write-Host ""

# Function to get Consul key
function Get-ConsulKey {
    param(
        [string]$Key,
        [string]$ConsulUrl
    )

    $fullKey = "TradingService/$Environment/$Key"
    $url = "$ConsulUrl/v1/kv/$fullKey"

    try {
        $response = Invoke-RestMethod -Uri $url -Method Get
        if ($response -and $response.Count -gt 0) {
            $decodedValue = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($response[0].Value))
            return $decodedValue
        }
    }
    catch {
        return $null
    }

    return $null
}

# Function to mask sensitive values
function Mask-Sensitive {
    param([string]$Value)

    if ([string]::IsNullOrEmpty($Value)) {
        return "(not set)"
    }

    if ($ShowSensitive) {
        return $Value
    }

    $visibleChars = [Math]::Min(8, $Value.Length)
    return $Value.Substring(0, $visibleChars) + "***"
}

# Test Consul connectivity
Write-Host "Testing Consul connectivity..." -ForegroundColor Cyan
try {
    $consulStatus = Invoke-RestMethod -Uri "$ConsulHost/v1/status/leader" -Method Get
    Write-Host "Consul is reachable" -ForegroundColor Green
}
catch {
    Write-Error "Cannot reach Consul at $ConsulHost"
    exit 1
}

Write-Host ""
Write-Host "=== Configuration from Consul ===" -ForegroundColor Cyan
Write-Host ""

# Display configuration
Write-Host "--- General Settings ---" -ForegroundColor Yellow
Write-Host "ScanTime: $(Get-ConsulKey -Key 'AppSettings:ScanTime' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "Watchlist: $(Get-ConsulKey -Key 'AppSettings:Watchlist' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host ""

Write-Host "--- Strategy Settings ---" -ForegroundColor Yellow
Write-Host "MinExpiryDays: $(Get-ConsulKey -Key 'AppSettings:Strategy:MinExpiryDays' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "MaxExpiryDays: $(Get-ConsulKey -Key 'AppSettings:Strategy:MaxExpiryDays' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "MinConfidence: $(Get-ConsulKey -Key 'AppSettings:Strategy:MinConfidence' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host ""

Write-Host "--- Broker Settings ---" -ForegroundColor Yellow
Write-Host "DefaultBroker: $(Get-ConsulKey -Key 'AppSettings:Broker:DefaultBroker' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "Exante Environment: $(Get-ConsulKey -Key 'AppSettings:Broker:Exante:Environment' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "Exante BaseUrl: $(Get-ConsulKey -Key 'AppSettings:Broker:Exante:BaseUrl' -ConsulUrl $ConsulHost)" -ForegroundColor White

$apiKey = Get-ConsulKey -Key 'AppSettings:Broker:Exante:ApiKey' -ConsulUrl $ConsulHost
Write-Host "Exante ApiKey: $(Mask-Sensitive $apiKey)" -ForegroundColor White

$apiSecret = Get-ConsulKey -Key 'AppSettings:Broker:Exante:ApiSecret' -ConsulUrl $ConsulHost
Write-Host "Exante ApiSecret: $(Mask-Sensitive $apiSecret)" -ForegroundColor White

$accountId = Get-ConsulKey -Key 'AppSettings:Broker:Exante:AccountId' -ConsulUrl $ConsulHost
Write-Host "Exante AccountId: $(Mask-Sensitive $accountId)" -ForegroundColor White

$jwtToken = Get-ConsulKey -Key 'AppSettings:Broker:Exante:JwtToken' -ConsulUrl $ConsulHost
Write-Host "Exante JwtToken: $(Mask-Sensitive $jwtToken)" -ForegroundColor White
Write-Host ""

Write-Host "--- Options Discovery Settings ---" -ForegroundColor Yellow
Write-Host "Enabled: $(Get-ConsulKey -Key 'AppSettings:OptionsDiscovery:Enabled' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "MinOpenInterest: $(Get-ConsulKey -Key 'AppSettings:OptionsDiscovery:MinOpenInterest' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "MinVolume: $(Get-ConsulKey -Key 'AppSettings:OptionsDiscovery:MinVolume' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "SampleOptionsPerUnderlying: $(Get-ConsulKey -Key 'AppSettings:OptionsDiscovery:SampleOptionsPerUnderlying' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "FallbackToWatchlist: $(Get-ConsulKey -Key 'AppSettings:OptionsDiscovery:FallbackToWatchlist' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "IncludeCallOptions: $(Get-ConsulKey -Key 'AppSettings:OptionsDiscovery:IncludeCallOptions' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host "MaxExpiryDays: $(Get-ConsulKey -Key 'AppSettings:OptionsDiscovery:MaxExpiryDays' -ConsulUrl $ConsulHost)" -ForegroundColor White
Write-Host ""

Write-Host "--- Database Settings ---" -ForegroundColor Yellow
$connString = Get-ConsulKey -Key 'AppSettings:Database:ConnectionString' -ConsulUrl $ConsulHost
Write-Host "ConnectionString: $connString" -ForegroundColor White
Write-Host ""

if (-not $ShowSensitive) {
    Write-Host "Note: Sensitive values are masked. Use -ShowSensitive to view full values." -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Consul UI: $ConsulHost/ui/dc1/kv/TradingService/$Environment/" -ForegroundColor Cyan
