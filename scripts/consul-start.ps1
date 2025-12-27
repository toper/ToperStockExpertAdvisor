# Start Consul in Development Mode
# This script starts Consul for local development

param(
    [switch]$Docker,
    [string]$DataDir = "consul-data"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Starting Consul ===" -ForegroundColor Cyan

if ($Docker) {
    # Start Consul using Docker
    Write-Host "Starting Consul using Docker..." -ForegroundColor Yellow

    # Check if Docker is available
    try {
        docker --version | Out-Null
    }
    catch {
        Write-Error "Docker is not installed or not in PATH"
        exit 1
    }

    # Check if Consul container already exists
    $existingContainer = docker ps -a --filter "name=consul-dev" --format "{{.Names}}"

    if ($existingContainer) {
        Write-Host "Consul container already exists. Starting it..." -ForegroundColor Yellow
        docker start consul-dev
    }
    else {
        Write-Host "Creating new Consul container..." -ForegroundColor Yellow
        docker run -d `
            --name consul-dev `
            -p 8500:8500 `
            -p 8600:8600/udp `
            -e CONSUL_BIND_INTERFACE=eth0 `
            consul:latest agent -dev -ui -client=0.0.0.0
    }

    Write-Host ""
    Write-Host "Consul started successfully!" -ForegroundColor Green
    Write-Host "UI available at: http://localhost:8500/ui" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "To stop Consul:" -ForegroundColor Yellow
    Write-Host "  docker stop consul-dev" -ForegroundColor White
    Write-Host ""
    Write-Host "To remove Consul:" -ForegroundColor Yellow
    Write-Host "  docker rm -f consul-dev" -ForegroundColor White
}
else {
    # Start Consul using local binary
    Write-Host "Starting Consul using local binary..." -ForegroundColor Yellow

    # Check if Consul is installed
    try {
        $consulVersion = consul version 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "Consul not found"
        }
        Write-Host "Found Consul: $($consulVersion[0])" -ForegroundColor Green
    }
    catch {
        Write-Host ""
        Write-Host "Consul is not installed. Please install it:" -ForegroundColor Red
        Write-Host "  Option 1 (Docker): .\scripts\consul-start.ps1 -Docker" -ForegroundColor Yellow
        Write-Host "  Option 2 (Manual): Download from https://www.consul.io/downloads" -ForegroundColor Yellow
        Write-Host "  Option 3 (Chocolatey): choco install consul" -ForegroundColor Yellow
        Write-Host ""
        exit 1
    }

    # Create data directory
    $scriptDir = Split-Path -Parent $PSScriptRoot
    $dataPath = Join-Path $scriptDir $DataDir

    if (-not (Test-Path $dataPath)) {
        New-Item -ItemType Directory -Path $dataPath | Out-Null
        Write-Host "Created data directory: $dataPath" -ForegroundColor Green
    }

    Write-Host "Starting Consul in development mode..." -ForegroundColor Yellow
    Write-Host "Data directory: $dataPath" -ForegroundColor Gray
    Write-Host ""

    # Start Consul in development mode
    Start-Process consul -ArgumentList "agent -dev -ui -data-dir=`"$dataPath`"" -NoNewWindow

    # Wait for Consul to start
    Write-Host "Waiting for Consul to start..." -ForegroundColor Yellow
    Start-Sleep -Seconds 2

    # Check if Consul is running
    try {
        $response = Invoke-RestMethod -Uri "http://localhost:8500/v1/status/leader" -Method Get -TimeoutSec 5
        Write-Host ""
        Write-Host "Consul started successfully!" -ForegroundColor Green
        Write-Host "UI available at: http://localhost:8500/ui" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "To stop Consul:" -ForegroundColor Yellow
        Write-Host "  Get-Process consul | Stop-Process" -ForegroundColor White
    }
    catch {
        Write-Warning "Could not verify Consul status. It may still be starting..."
    }
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Load configuration: .\scripts\consul-load-config.ps1" -ForegroundColor White
Write-Host "  2. View configuration: .\scripts\consul-get-config.ps1" -ForegroundColor White
Write-Host "  3. Run service: dotnet run --project src\TradingService" -ForegroundColor White
Write-Host ""
