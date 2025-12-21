# run-local.ps1
# Runs all services locally for development

param(
    [switch]$ApiOnly,
    [switch]$WorkerOnly,
    [switch]$FrontendOnly,
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$rootDir = Split-Path -Parent $PSScriptRoot

Write-Host "=== Toper Stock Expert Advisor - Local Development ===" -ForegroundColor Cyan
Write-Host ""

# Build if not skipped
if (-not $NoBuild) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    Push-Location $rootDir
    try {
        dotnet build
        if ($LASTEXITCODE -ne 0) { throw "Build failed" }
        Write-Host "[OK] Build successful" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

$jobs = @()

# Start API
if (-not $WorkerOnly -and -not $FrontendOnly) {
    Write-Host "Starting TradingService.Api..." -ForegroundColor Yellow
    $apiJob = Start-Job -Name "TradingApi" -ScriptBlock {
        param($dir)
        Set-Location $dir
        dotnet run --project src/TradingService.Api --no-build
    } -ArgumentList $rootDir
    $jobs += $apiJob
    Write-Host "[OK] API starting on http://localhost:5001" -ForegroundColor Green
}

# Start Worker (optional - usually you don't need it for development)
if ($WorkerOnly -or (-not $ApiOnly -and -not $FrontendOnly -and $env:START_WORKER -eq "true")) {
    Write-Host "Starting TradingService Worker..." -ForegroundColor Yellow
    $workerJob = Start-Job -Name "TradingWorker" -ScriptBlock {
        param($dir)
        Set-Location $dir
        dotnet run --project src/TradingService --no-build
    } -ArgumentList $rootDir
    $jobs += $workerJob
    Write-Host "[OK] Worker starting" -ForegroundColor Green
}

# Start Frontend
if (-not $ApiOnly -and -not $WorkerOnly) {
    Write-Host "Starting Frontend..." -ForegroundColor Yellow
    $frontendJob = Start-Job -Name "Frontend" -ScriptBlock {
        param($dir)
        Set-Location (Join-Path $dir "frontend")
        npm run dev
    } -ArgumentList $rootDir
    $jobs += $frontendJob
    Write-Host "[OK] Frontend starting on http://localhost:5173" -ForegroundColor Green
}

Write-Host ""
Write-Host "=== All services starting ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Services:" -ForegroundColor Yellow
Write-Host "  API:      http://localhost:5001" -ForegroundColor White
Write-Host "  Swagger:  http://localhost:5001/swagger" -ForegroundColor White
Write-Host "  Frontend: http://localhost:5173" -ForegroundColor White
Write-Host ""
Write-Host "Press Ctrl+C to stop all services" -ForegroundColor Yellow
Write-Host ""

try {
    # Wait for all jobs and show output
    while ($true) {
        foreach ($job in $jobs) {
            $output = Receive-Job -Job $job -ErrorAction SilentlyContinue
            if ($output) {
                $color = switch ($job.Name) {
                    "TradingApi" { "Cyan" }
                    "TradingWorker" { "Magenta" }
                    "Frontend" { "Green" }
                    default { "White" }
                }
                Write-Host "[$($job.Name)] $output" -ForegroundColor $color
            }
        }
        Start-Sleep -Milliseconds 500
    }
}
finally {
    Write-Host ""
    Write-Host "Stopping all services..." -ForegroundColor Yellow
    $jobs | Stop-Job -PassThru | Remove-Job
    Write-Host "[OK] All services stopped" -ForegroundColor Green
}
