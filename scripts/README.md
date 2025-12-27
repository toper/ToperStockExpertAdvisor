# Scripts Directory

PowerShell scripts for managing TradingService configuration and Consul integration.

## Quick Start

### Windows (PowerShell)
```powershell
# 1. Test configuration setup
.\scripts\test-configuration.ps1

# 2. Start Consul (if needed)
.\scripts\consul-start.ps1 -Docker

# 3. Load configuration to Consul
.\scripts\consul-load-config.ps1

# 4. View configuration
.\scripts\consul-get-config.ps1

# 5. Run the service
dotnet run --project src\TradingService
```

### Linux/Mac (Bash)
```bash
# 1. Start Consul with Docker
docker run -d --name consul-dev -p 8500:8500 consul:latest

# 2. Load configuration to Consul
chmod +x scripts/consul-load-config.sh
./scripts/consul-load-config.sh

# 3. Run the service
dotnet run --project src/TradingService
```

---

## Scripts

### test-configuration.ps1

Tests the complete configuration loading process.

**What it checks**:
- ✓ .env file exists and has required variables
- ✓ appsettings.json exists and sensitive fields are empty
- ✓ Consul is running (optional)
- ✓ Configuration loaded to Consul (optional)
- ✓ .env variables load correctly
- ✓ Project builds successfully

**Usage**:
```powershell
# Full test (including Consul)
.\scripts\test-configuration.ps1

# Test without Consul
.\scripts\test-configuration.ps1 -SkipConsul
```

---

### consul-start.ps1

Starts Consul for local development.

**Options**:
- Docker mode (recommended): Runs Consul in Docker container
- Local mode: Uses local Consul binary

**Usage**:
```powershell
# Start with Docker (recommended)
.\scripts\consul-start.ps1 -Docker

# Start with local binary
.\scripts\consul-start.ps1

# Custom data directory
.\scripts\consul-start.ps1 -DataDir "my-consul-data"
```

**Stopping Consul**:
```powershell
# Docker
docker stop consul-dev

# Local
Get-Process consul | Stop-Process
```

---

### consul-load-config.ps1

Loads configuration from appsettings.json and .env to Consul KV store.

**What it does**:
- Reads non-sensitive settings from appsettings.json
- Reads sensitive credentials from .env
- Uploads to Consul at `TradingService/{Environment}/AppSettings/*`
- Masks sensitive values in output

**Usage**:
```powershell
# Load to Production environment
.\scripts\consul-load-config.ps1

# Load to Staging environment
.\scripts\consul-load-config.ps1 -Environment "Staging"

# Use different Consul server
.\scripts\consul-load-config.ps1 -ConsulHost "http://consul.example.com:8500"

# Test without making changes
.\scripts\consul-load-config.ps1 -DryRun

# Use different .env file
.\scripts\consul-load-config.ps1 -EnvFile ".env.production"
```

**Parameters**:
- `-ConsulHost` - Consul server URL (default: http://localhost:8500)
- `-Environment` - Target environment (default: Production)
- `-EnvFile` - Path to .env file (default: .env)
- `-DryRun` - Test mode, doesn't upload to Consul

**Example output**:
```
=== Consul Configuration Loader ===
Consul Host: http://localhost:8500
Environment: Production

Loading environment variables from: D:\GIT\ToperStockExpertAdvisor\.env
Loaded 8 environment variables

=== Uploading Configuration to Consul ===

--- Non-Sensitive Settings ---
[OK] Set: TradingService/Production/AppSettings:ScanTime
[OK] Set: TradingService/Production/AppSettings:Watchlist
...

--- Sensitive Settings (from .env) ---
Uploading EXANTE_API_KEY (3a9fa8a6***)...
[OK] Set: TradingService/Production/AppSettings:Broker:Exante:ApiKey
...

=== Configuration Upload Complete ===
```

---

### consul-get-config.ps1

Displays configuration stored in Consul KV store.

**What it shows**:
- General settings (ScanTime, Watchlist)
- Strategy settings
- Broker settings (with masked sensitive values)
- Options Discovery settings
- Database settings

**Usage**:
```powershell
# View configuration (sensitive values masked)
.\scripts\consul-get-config.ps1

# View with full sensitive values
.\scripts\consul-get-config.ps1 -ShowSensitive

# View Staging environment
.\scripts\consul-get-config.ps1 -Environment "Staging"

# Different Consul server
.\scripts\consul-get-config.ps1 -ConsulHost "http://consul.example.com:8500"
```

**Parameters**:
- `-ConsulHost` - Consul server URL (default: http://localhost:8500)
- `-Environment` - Target environment (default: Production)
- `-ShowSensitive` - Show full sensitive values instead of masking

**Example output**:
```
=== Consul Configuration Viewer ===

--- General Settings ---
ScanTime: 04:00
Watchlist: SPY,QQQ,AAPL,MSFT,GOOGL,NVDA,AMD,TSLA

--- Broker Settings ---
Exante ApiKey: 3a9fa8a6***
Exante ApiSecret: ***hidden***
Exante JwtToken: ***hidden***
...
```

---

### consul-load-config.sh (Linux/Mac)

Bash version of consul-load-config.ps1 - loads configuration from appsettings.json and .env to Consul KV store.

**Usage**:
```bash
# Load to Production environment
./scripts/consul-load-config.sh

# Load to Staging environment
./scripts/consul-load-config.sh --environment Staging

# Use different Consul server
./scripts/consul-load-config.sh --consul-host "http://consul.example.com:8500"

# Test without making changes
./scripts/consul-load-config.sh --dry-run

# Use different .env file
./scripts/consul-load-config.sh --env-file .env.production
```

**Options**:
- `--consul-host URL` - Consul server URL (default: http://localhost:8500)
- `--environment NAME` - Target environment (default: Production)
- `--env-file PATH` - Path to .env file (default: .env)
- `--dry-run` - Test mode, doesn't upload to Consul
- `--help` - Show help message

**Requirements**:
- `jq` - JSON parser (install with: `sudo apt-get install jq` or `brew install jq`)
- `curl` - HTTP client (usually pre-installed)

---

## Configuration Sources Priority

1. **System Environment Variables** (highest priority)
2. **.env file**
3. **Consul KV Store**
4. **appsettings.json** (lowest priority)

## Typical Workflows

### Initial Setup

```powershell
# 1. Create .env file from template
cp .env.example .env
notepad .env  # Add your credentials

# 2. Test configuration
.\scripts\test-configuration.ps1

# 3. Start Consul
.\scripts\consul-start.ps1 -Docker

# 4. Load config to Consul
.\scripts\consul-load-config.ps1

# 5. Verify
.\scripts\consul-get-config.ps1
```

### Update Configuration

```powershell
# 1. Update .env or appsettings.json
notepad .env

# 2. Reload to Consul
.\scripts\consul-load-config.ps1

# 3. Service picks up changes automatically (no restart needed)
```

### Deploy to New Environment

```powershell
# 1. Create environment-specific .env
cp .env.example .env.staging
notepad .env.staging

# 2. Load to Consul
.\scripts\consul-load-config.ps1 -Environment "Staging" -EnvFile ".env.staging"

# 3. Deploy service with ASPNETCORE_ENVIRONMENT=Staging
```

---

## Troubleshooting

### "Consul is not running"

```powershell
# Start Consul
.\scripts\consul-start.ps1 -Docker

# Or check if it's already running
curl http://localhost:8500/v1/status/leader
```

### ".env file not found"

```powershell
# Copy template
cp .env.example .env

# Edit with your credentials
notepad .env
```

### "Configuration not loaded to Consul"

```powershell
# Load configuration
.\scripts\consul-load-config.ps1

# Verify it was loaded
.\scripts\consul-get-config.ps1
```

### "Service can't connect to Consul"

Service will continue with local configuration (.env + appsettings.json). Check:

```powershell
# 1. Is Consul running?
curl http://localhost:8500/v1/status/leader

# 2. Is CONSUL_HOST set correctly?
$env:CONSUL_HOST  # Should be http://localhost:8500

# 3. Check service logs
# Look for: "Consul configuration source added: http://localhost:8500"
```

---

## Security Notes

⚠️ **IMPORTANT**:
- `.env` file contains sensitive credentials - **NEVER commit to Git**
- `.env` is in `.gitignore` by default
- Use `.env.example` as a template (no real credentials)
- For production, use Consul or Azure Key Vault for secrets

✅ **Best Practices**:
- Keep `.env` files local to each developer/server
- Rotate credentials regularly
- Use Consul ACLs in production
- Never log full sensitive values

---

## Additional Resources

- **Full Documentation**: `docs/CONFIGURATION_MANAGEMENT.md`
- **Consul UI**: http://localhost:8500/ui
- **Consul Docs**: https://www.consul.io/docs

---

## Script Dependencies

All scripts require:
- PowerShell 5.1 or higher
- .NET 10 SDK (for building/running service)

Optional:
- Docker (for `consul-start.ps1 -Docker`)
- Consul binary (for `consul-start.ps1` without Docker)
- dotnet-script (for `test-configuration.ps1` env loading test)
