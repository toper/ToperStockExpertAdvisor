# Test Configuration Loading
# This script tests the complete configuration loading process

param(
    [switch]$SkipConsul
)

$ErrorActionPreference = "Stop"

Write-Host "=== Configuration Loading Test ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check .env file
Write-Host "1. Checking .env file..." -ForegroundColor Yellow
$scriptDir = Split-Path -Parent $PSScriptRoot
$envPath = Join-Path $scriptDir ".env"

if (Test-Path $envPath) {
    Write-Host "   ✓ .env file exists" -ForegroundColor Green

    # Count non-empty, non-comment lines
    $envLines = Get-Content $envPath | Where-Object { $_ -and -not $_.Trim().StartsWith("#") }
    Write-Host "   ✓ Found $($envLines.Count) environment variables" -ForegroundColor Green

    # Check for required variables
    $requiredVars = @("EXANTE_API_KEY", "EXANTE_API_SECRET", "EXANTE_ACCOUNT_ID", "EXANTE_JWT_TOKEN")
    $envContent = Get-Content $envPath -Raw

    foreach ($varName in $requiredVars) {
        if ($envContent -match "$varName=") {
            Write-Host "   ✓ $varName is set" -ForegroundColor Green
        }
        else {
            Write-Host "   ✗ $varName is missing" -ForegroundColor Red
        }
    }
}
else {
    Write-Host "   ✗ .env file not found" -ForegroundColor Red
    Write-Host "     Run: cp .env.example .env" -ForegroundColor Yellow
}

Write-Host ""

# Test 2: Check appsettings.json
Write-Host "2. Checking appsettings.json..." -ForegroundColor Yellow
$appsettingsPath = Join-Path $scriptDir "src\TradingService\appsettings.json"

if (Test-Path $appsettingsPath) {
    Write-Host "   ✓ appsettings.json exists" -ForegroundColor Green

    $appSettings = Get-Content $appsettingsPath | ConvertFrom-Json

    # Verify sensitive fields are empty
    if ([string]::IsNullOrEmpty($appSettings.AppSettings.Broker.Exante.ApiKey)) {
        Write-Host "   ✓ ApiKey is empty (good - should be in .env)" -ForegroundColor Green
    }
    else {
        Write-Host "   ✗ ApiKey is in appsettings.json (should be in .env)" -ForegroundColor Red
    }

    if ([string]::IsNullOrEmpty($appSettings.AppSettings.Broker.Exante.ApiSecret)) {
        Write-Host "   ✓ ApiSecret is empty (good - should be in .env)" -ForegroundColor Green
    }
    else {
        Write-Host "   ✗ ApiSecret is in appsettings.json (should be in .env)" -ForegroundColor Red
    }
}
else {
    Write-Host "   ✗ appsettings.json not found" -ForegroundColor Red
}

Write-Host ""

# Test 3: Check Consul (if not skipped)
if (-not $SkipConsul) {
    Write-Host "3. Checking Consul..." -ForegroundColor Yellow

    try {
        $response = Invoke-RestMethod -Uri "http://localhost:8500/v1/status/leader" -Method Get -TimeoutSec 2
        Write-Host "   ✓ Consul is running" -ForegroundColor Green

        # Check if configuration exists in Consul
        try {
            $scanTime = Invoke-RestMethod -Uri "http://localhost:8500/v1/kv/TradingService/Production/AppSettings:ScanTime" -Method Get
            if ($scanTime) {
                Write-Host "   ✓ Configuration exists in Consul" -ForegroundColor Green
            }
        }
        catch {
            Write-Host "   ⚠ Configuration not loaded to Consul yet" -ForegroundColor Yellow
            Write-Host "     Run: .\scripts\consul-load-config.ps1" -ForegroundColor Cyan
        }
    }
    catch {
        Write-Host "   ⚠ Consul is not running" -ForegroundColor Yellow
        Write-Host "     Run: .\scripts\consul-start.ps1 -Docker" -ForegroundColor Cyan
        Write-Host "     (Service will work without Consul using .env)" -ForegroundColor Gray
    }
}
else {
    Write-Host "3. Skipping Consul check (use -SkipConsul:$false to enable)" -ForegroundColor Gray
}

Write-Host ""

# Test 4: Test .env loading with DotNetEnv
Write-Host "4. Testing .env loading..." -ForegroundColor Yellow

$testScript = @'
using System;

// Load .env file
DotNetEnv.Env.Load();

// Check if variables are loaded
var apiKey = Environment.GetEnvironmentVariable("EXANTE_API_KEY");
var apiSecret = Environment.GetEnvironmentVariable("EXANTE_API_SECRET");

if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
{
    Console.WriteLine("✓ Environment variables loaded successfully");
    Console.WriteLine($"✓ API Key: {apiKey.Substring(0, Math.Min(8, apiKey.Length))}***");
    return 0;
}
else
{
    Console.WriteLine("✗ Environment variables not loaded");
    return 1;
}
'@

$testScriptPath = Join-Path $env:TEMP "test-env-loading.cs"
$testScript | Out-File -FilePath $testScriptPath -Encoding UTF8

try {
    Push-Location $scriptDir
    $output = dotnet script $testScriptPath 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "   $output" -ForegroundColor Green
    }
    else {
        Write-Host "   ✗ Failed to load environment variables" -ForegroundColor Red
        Write-Host "     $output" -ForegroundColor Gray
    }
}
catch {
    Write-Host "   ⚠ Could not test .env loading (dotnet-script not installed)" -ForegroundColor Yellow
    Write-Host "     Install with: dotnet tool install -g dotnet-script" -ForegroundColor Cyan
}
finally {
    Pop-Location
    Remove-Item $testScriptPath -ErrorAction SilentlyContinue
}

Write-Host ""

# Test 5: Build project
Write-Host "5. Building project..." -ForegroundColor Yellow

try {
    Push-Location $scriptDir
    $buildOutput = dotnet build --nologo --verbosity quiet 2>&1

    if ($LASTEXITCODE -eq 0) {
        Write-Host "   ✓ Project builds successfully" -ForegroundColor Green
    }
    else {
        Write-Host "   ✗ Build failed" -ForegroundColor Red
        Write-Host "     $buildOutput" -ForegroundColor Gray
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "=== Test Summary ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration Priority (highest to lowest):" -ForegroundColor Yellow
Write-Host "  1. System Environment Variables" -ForegroundColor White
Write-Host "  2. .env file" -ForegroundColor White
Write-Host "  3. Consul KV Store" -ForegroundColor White
Write-Host "  4. appsettings.json" -ForegroundColor White
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  • Ensure .env file has correct credentials" -ForegroundColor White
Write-Host "  • Start Consul: .\scripts\consul-start.ps1 -Docker" -ForegroundColor White
Write-Host "  • Load config to Consul: .\scripts\consul-load-config.ps1" -ForegroundColor White
Write-Host "  • Run service: dotnet run --project src/TradingService" -ForegroundColor White
Write-Host ""
