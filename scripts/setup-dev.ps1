# setup-dev.ps1
# Sets up the development environment for Toper Stock Expert Advisor

param(
    [switch]$SkipRestore,
    [switch]$SkipNpm
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot

Write-Host "=== Toper Stock Expert Advisor - Development Setup ===" -ForegroundColor Cyan
Write-Host ""

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# .NET SDK
$dotnetVersion = dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] .NET SDK not found. Please install .NET 10 SDK." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] .NET SDK: $dotnetVersion" -ForegroundColor Green

# Node.js
$nodeVersion = node --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Node.js not found. Please install Node.js 22+." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] Node.js: $nodeVersion" -ForegroundColor Green

# npm
$npmVersion = npm --version 2>$null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] npm not found." -ForegroundColor Red
    exit 1
}
Write-Host "[OK] npm: $npmVersion" -ForegroundColor Green

Write-Host ""

# Restore .NET packages
if (-not $SkipRestore) {
    Write-Host "Restoring .NET packages..." -ForegroundColor Yellow
    Push-Location $rootDir
    try {
        dotnet restore
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed" }
        Write-Host "[OK] .NET packages restored" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# Install npm packages
if (-not $SkipNpm) {
    Write-Host "Installing npm packages..." -ForegroundColor Yellow
    Push-Location (Join-Path $rootDir "frontend")
    try {
        npm install
        if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
        Write-Host "[OK] npm packages installed" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# Build solution
Write-Host "Building .NET solution..." -ForegroundColor Yellow
Push-Location $rootDir
try {
    dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed" }
    Write-Host "[OK] Solution built successfully" -ForegroundColor Green
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "=== Setup Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "To run the application locally:" -ForegroundColor Yellow
Write-Host "  1. Start the API:      dotnet run --project src/TradingService.Api" -ForegroundColor White
Write-Host "  2. Start the Worker:   dotnet run --project src/TradingService" -ForegroundColor White
Write-Host "  3. Start the Frontend: cd frontend && npm run dev" -ForegroundColor White
Write-Host ""
Write-Host "Or use Docker:" -ForegroundColor Yellow
Write-Host "  cd docker && docker-compose up -d" -ForegroundColor White
Write-Host ""
