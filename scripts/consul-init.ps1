# consul-init.ps1
# Initializes Consul KV store with default configuration
# Run this after starting Consul to set up the configuration

param(
    [string]$ConsulHost = "http://localhost:8500"
)

Write-Host "Initializing Consul KV store at $ConsulHost" -ForegroundColor Cyan

# Trading Service Configuration
$tradingConfig = @{
    ScanTime = "04:00"
    Watchlist = @("SPY", "QQQ", "AAPL", "MSFT", "GOOGL", "NVDA", "AMD", "TSLA")
    Strategy = @{
        MinExpiryDays = 14
        MaxExpiryDays = 21
        MinConfidence = 0.6
    }
    Consul = @{
        Host = "http://localhost:8500"
        ServiceName = "TradingService"
        ServicePort = 5000
    }
    Database = @{
        ConnectionString = "Data Source=trading.db"
    }
} | ConvertTo-Json -Depth 3

try {
    Invoke-RestMethod -Uri "$ConsulHost/v1/kv/config/TradingService/appsettings" -Method PUT -Body $tradingConfig -ContentType "application/json"
    Write-Host "[OK] Trading Service config stored" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to store Trading Service config: $_" -ForegroundColor Red
}

# Trading API Configuration
$apiConfig = @{
    Consul = @{
        Host = "http://localhost:8500"
        ServiceName = "TradingApi"
        ServicePort = 5001
    }
    Database = @{
        ConnectionString = "Data Source=trading.db"
    }
} | ConvertTo-Json -Depth 3

try {
    Invoke-RestMethod -Uri "$ConsulHost/v1/kv/config/TradingApi/appsettings" -Method PUT -Body $apiConfig -ContentType "application/json"
    Write-Host "[OK] Trading API config stored" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to store Trading API config: $_" -ForegroundColor Red
}

# Broker Configuration (Exante)
$brokerConfig = @{
    ApiKey = ""
    ApiSecret = ""
    AccountId = ""
    Environment = "Demo"
    BaseUrl = "https://api-demo.exante.eu"
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "$ConsulHost/v1/kv/TradingService/brokers/Exante" -Method PUT -Body $brokerConfig -ContentType "application/json"
    Write-Host "[OK] Exante broker config stored (credentials empty - update manually)" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to store broker config: $_" -ForegroundColor Red
}

# Loki Configuration
$lokiConfig = @{
    Endpoint = "http://loki:3100"
    Username = ""
    Password = ""
} | ConvertTo-Json

try {
    Invoke-RestMethod -Uri "$ConsulHost/v1/kv/TradingService/logging/loki" -Method PUT -Body $lokiConfig -ContentType "application/json"
    Write-Host "[OK] Loki config stored" -ForegroundColor Green
}
catch {
    Write-Host "[ERROR] Failed to store Loki config: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Consul initialization complete!" -ForegroundColor Cyan
Write-Host "Access Consul UI at: $ConsulHost/ui" -ForegroundColor Yellow
Write-Host ""
Write-Host "IMPORTANT: Update broker credentials in Consul KV before using live trading!" -ForegroundColor Red
