#!/bin/bash
# Consul Configuration Loader for TradingService
# This script loads configuration from appsettings.json and .env to Consul KV store

set -e

# Default parameters
CONSUL_HOST="${CONSUL_HOST:-http://localhost:8500}"
ENVIRONMENT="${ENVIRONMENT:-Production}"
ENV_FILE="${ENV_FILE:-.env}"
DRY_RUN="${DRY_RUN:-false}"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --consul-host)
            CONSUL_HOST="$2"
            shift 2
            ;;
        --environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        --env-file)
            ENV_FILE="$2"
            shift 2
            ;;
        --dry-run)
            DRY_RUN=true
            shift
            ;;
        --help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --consul-host URL    Consul server URL (default: http://localhost:8500)"
            echo "  --environment NAME   Target environment (default: Production)"
            echo "  --env-file PATH      Path to .env file (default: .env)"
            echo "  --dry-run            Test mode, doesn't upload to Consul"
            echo "  --help               Show this help message"
            echo ""
            echo "Environment variables:"
            echo "  CONSUL_HOST          Override Consul server URL"
            echo "  ENVIRONMENT          Override target environment"
            echo "  ENV_FILE             Override .env file path"
            echo "  DRY_RUN              Set to 'true' for dry-run mode"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

echo -e "${CYAN}=== Consul Configuration Loader ===${NC}"
echo -e "${GRAY}Consul Host: $CONSUL_HOST${NC}"
echo -e "${GRAY}Environment: $ENVIRONMENT${NC}"
echo -e "${GRAY}Env File: $ENV_FILE${NC}"
echo ""

# Get script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"

# Function to load .env file
load_env_file() {
    local env_path="$1"

    if [[ ! -f "$env_path" ]]; then
        echo -e "${YELLOW}Warning: Env file not found: $env_path${NC}"
        return
    fi

    # Load .env into associative array
    while IFS='=' read -r key value; do
        # Skip comments and empty lines
        [[ $key =~ ^#.*$ ]] && continue
        [[ -z $key ]] && continue

        # Remove leading/trailing whitespace
        key=$(echo "$key" | xargs)
        value=$(echo "$value" | xargs)

        # Export to environment
        export "$key=$value"
    done < "$env_path"
}

# Function to set Consul key
set_consul_key() {
    local key="$1"
    local value="$2"

    local full_key="TradingService/$ENVIRONMENT/$key"
    local url="$CONSUL_HOST/v1/kv/$full_key"

    if [[ "$DRY_RUN" == "true" ]]; then
        echo -e "${YELLOW}[DRY-RUN] Would set: $full_key = $value${NC}"
        return
    fi

    if curl -s -X PUT -d "$value" "$url" > /dev/null 2>&1; then
        echo -e "${GREEN}[OK] Set: $full_key${NC}"
    else
        echo -e "${RED}[ERROR] Failed to set: $full_key${NC}"
        return 1
    fi
}

# Load environment variables from .env
ENV_FILE_PATH="$ROOT_DIR/$ENV_FILE"

echo -e "${CYAN}Loading environment variables from: $ENV_FILE_PATH${NC}"
load_env_file "$ENV_FILE_PATH"

if [[ -f "$ENV_FILE_PATH" ]]; then
    ENV_COUNT=$(grep -c "^[^#].*=" "$ENV_FILE_PATH" 2>/dev/null || echo "0")
    echo -e "${GREEN}Loaded $ENV_COUNT environment variables${NC}"
else
    echo -e "${YELLOW}No environment variables loaded from $ENV_FILE${NC}"
fi

# Load appsettings.json
APPSETTINGS_PATH="$ROOT_DIR/src/TradingService/appsettings.json"
echo -e "${CYAN}Loading configuration from: $APPSETTINGS_PATH${NC}"

if [[ ! -f "$APPSETTINGS_PATH" ]]; then
    echo -e "${RED}Error: appsettings.json not found at: $APPSETTINGS_PATH${NC}"
    exit 1
fi

# Check if jq is installed
if ! command -v jq &> /dev/null; then
    echo -e "${RED}Error: jq is not installed. Please install jq to parse JSON.${NC}"
    echo "Install with: sudo apt-get install jq (Debian/Ubuntu) or brew install jq (macOS)"
    exit 1
fi

# Test Consul connectivity
echo -e "${CYAN}Testing Consul connectivity...${NC}"
if curl -s "$CONSUL_HOST/v1/status/leader" > /dev/null 2>&1; then
    echo -e "${GREEN}Consul is reachable${NC}"
else
    echo -e "${RED}Error: Cannot reach Consul at $CONSUL_HOST. Make sure Consul is running.${NC}"
    exit 1
fi

echo ""
echo -e "${CYAN}=== Uploading Configuration to Consul ===${NC}"
echo ""

# Upload non-sensitive settings from appsettings.json
echo -e "${YELLOW}--- Non-Sensitive Settings ---${NC}"

SCAN_TIME=$(jq -r '.AppSettings.ScanTime' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:ScanTime" "$SCAN_TIME"

WATCHLIST=$(jq -r '.AppSettings.Watchlist | join(",")' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:Watchlist" "$WATCHLIST"

# Strategy settings
MIN_EXPIRY=$(jq -r '.AppSettings.Strategy.MinExpiryDays' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:Strategy:MinExpiryDays" "$MIN_EXPIRY"

MAX_EXPIRY=$(jq -r '.AppSettings.Strategy.MaxExpiryDays' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:Strategy:MaxExpiryDays" "$MAX_EXPIRY"

MIN_CONFIDENCE=$(jq -r '.AppSettings.Strategy.MinConfidence' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:Strategy:MinConfidence" "$MIN_CONFIDENCE"

# Consul settings
CONSUL_SERVICE_HOST=$(jq -r '.AppSettings.Consul.Host' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:Consul:Host" "$CONSUL_SERVICE_HOST"

CONSUL_SERVICE_NAME=$(jq -r '.AppSettings.Consul.ServiceName' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:Consul:ServiceName" "$CONSUL_SERVICE_NAME"

# Broker non-sensitive settings
DEFAULT_BROKER=$(jq -r '.AppSettings.Broker.DefaultBroker' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:Broker:DefaultBroker" "$DEFAULT_BROKER"

EXANTE_ENV=$(jq -r '.AppSettings.Broker.Exante.Environment' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:Broker:Exante:Environment" "$EXANTE_ENV"

EXANTE_BASE_URL=$(jq -r '.AppSettings.Broker.Exante.BaseUrl' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:Broker:Exante:BaseUrl" "$EXANTE_BASE_URL"

# Options Discovery settings
OD_ENABLED=$(jq -r '.AppSettings.OptionsDiscovery.Enabled' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:OptionsDiscovery:Enabled" "$OD_ENABLED"

OD_MIN_OI=$(jq -r '.AppSettings.OptionsDiscovery.MinOpenInterest' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:OptionsDiscovery:MinOpenInterest" "$OD_MIN_OI"

OD_MIN_VOL=$(jq -r '.AppSettings.OptionsDiscovery.MinVolume' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:OptionsDiscovery:MinVolume" "$OD_MIN_VOL"

OD_SAMPLE=$(jq -r '.AppSettings.OptionsDiscovery.SampleOptionsPerUnderlying' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:OptionsDiscovery:SampleOptionsPerUnderlying" "$OD_SAMPLE"

OD_FALLBACK=$(jq -r '.AppSettings.OptionsDiscovery.FallbackToWatchlist' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:OptionsDiscovery:FallbackToWatchlist" "$OD_FALLBACK"

OD_CALLS=$(jq -r '.AppSettings.OptionsDiscovery.IncludeCallOptions' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:OptionsDiscovery:IncludeCallOptions" "$OD_CALLS"

OD_MAX_EXPIRY=$(jq -r '.AppSettings.OptionsDiscovery.MaxExpiryDays' "$APPSETTINGS_PATH")
set_consul_key "AppSettings:OptionsDiscovery:MaxExpiryDays" "$OD_MAX_EXPIRY"

echo ""
echo -e "${YELLOW}--- Sensitive Settings (from .env) ---${NC}"

# Upload sensitive settings from .env
if [[ -n "$EXANTE_API_KEY" ]]; then
    MASKED_KEY="${EXANTE_API_KEY:0:8}***"
    echo -e "${GRAY}Uploading EXANTE_API_KEY ($MASKED_KEY)...${NC}"
    set_consul_key "AppSettings:Broker:Exante:ApiKey" "$EXANTE_API_KEY"
fi

if [[ -n "$EXANTE_API_SECRET" ]]; then
    echo -e "${GRAY}Uploading EXANTE_API_SECRET (***hidden***)...${NC}"
    set_consul_key "AppSettings:Broker:Exante:ApiSecret" "$EXANTE_API_SECRET"
fi

if [[ -n "$EXANTE_ACCOUNT_ID" ]]; then
    MASKED_ACCOUNT="${EXANTE_ACCOUNT_ID:0:8}***"
    echo -e "${GRAY}Uploading EXANTE_ACCOUNT_ID ($MASKED_ACCOUNT)...${NC}"
    set_consul_key "AppSettings:Broker:Exante:AccountId" "$EXANTE_ACCOUNT_ID"
fi

if [[ -n "$EXANTE_JWT_TOKEN" ]]; then
    echo -e "${GRAY}Uploading EXANTE_JWT_TOKEN (***hidden***)...${NC}"
    set_consul_key "AppSettings:Broker:Exante:JwtToken" "$EXANTE_JWT_TOKEN"
fi

if [[ -n "$DATABASE_CONNECTION_STRING" ]]; then
    echo -e "${GRAY}Uploading DATABASE_CONNECTION_STRING...${NC}"
    set_consul_key "AppSettings:Database:ConnectionString" "$DATABASE_CONNECTION_STRING"
fi

echo ""
echo -e "${GREEN}=== Configuration Upload Complete ===${NC}"
echo ""
echo -e "${CYAN}You can view the configuration in Consul UI:${NC}"
echo -e "${NC}$CONSUL_HOST/ui/dc1/kv/TradingService/$ENVIRONMENT/${NC}"
echo ""

if [[ "$DRY_RUN" == "true" ]]; then
    echo -e "${YELLOW}This was a DRY-RUN. No changes were made to Consul.${NC}"
fi
