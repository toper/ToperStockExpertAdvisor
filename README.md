# Toper Stock Expert Advisor

Aplikacja do automatycznego wyszukiwania okazji do sprzedaży opcji PUT (short put) na instrumentach z silnym trendem wzrostowym. System analizuje rynek i generuje rekomendacje opcji PUT z datą zapadalności 2-3 tygodnie, minimalizując ryzyko wykonania (ITM).

## Funkcjonalności

- **Automatyczne skanowanie rynku**: Codzienny przegląd instrumentów z watchlisty
- **Wielopoziomowa analiza techniczna**: RSI, MACD, Moving Averages, analiza trendów
- **Strategie opcyjne**:
  - Short-Term PUT (główna strategia 2-3 tygodniowa)
  - Dividend Momentum
  - Volatility Crush
- **Dashboard Vue.js**: Wizualizacja rekomendacji z kalkulatorem zysków
- **Integracje**: Yahoo Finance (dane rynkowe), Exante (opcje i broker)

## Stack Technologiczny

| Warstwa | Technologia | Wersja |
|---------|-------------|--------|
| Backend | .NET 10 | 10.0 |
| Baza Danych | SQLite + Linq2DB | 5.4.1 |
| Service Discovery | HashiCorp Consul | 1.7.14.9 |
| API Gateway | Ocelot | 24.0.1 |
| Logowanie | NLog + Grafana Loki | 6.0.7 / 2.2.1 |
| Frontend | Vue.js 3 + Vite + Tailwind CSS | 3.x |
| Konteneryzacja | Docker + Docker Compose | - |

## Architektura

```
┌─────────────┐      ┌──────────────────┐      ┌─────────────┐
│   Vue.js    │─────▶│  TradingService  │─────▶│   Consul    │
│  Frontend   │      │    .Api + Ocelot │      │   (8500)    │
│   (5173)    │      │      (5001)      │      └─────────────┘
└─────────────┘      └──────────────────┘
                              │
                              ▼
                     ┌──────────────────┐      ┌─────────────┐
                     │  TradingService  │─────▶│   SQLite    │
                     │  Worker Service  │      │  trading.db │
                     └──────────────────┘      └─────────────┘
                              │
                     ┌────────┴────────┐
                     ▼                 ▼
              ┌────────────┐    ┌────────────┐
              │   Yahoo    │    │   Exante   │
              │  Finance   │    │    API     │
              └────────────┘    └────────────┘
```

## Uruchomienie w Dockerze

### Wymagania wstępne

- Docker Desktop (Windows/Mac) lub Docker Engine + Docker Compose (Linux)
- .NET SDK 10.0 (dla budowania lokalnie)
- Node.js 18+ (dla frontendu)

### Krok 1: Konfiguracja zmiennych środowiskowych

Skopiuj przykładowy plik `.env` i dostosuj wartości:

```bash
cp .env.example .env
```

Edytuj `.env` i ustaw:
```bash
# Consul
CONSUL_HOST=http://consul:8500

# Exante API (opcjonalnie - dla demo można zostawić puste)
AppSettings__Broker__Exante__ApiKey=your-api-key
AppSettings__Broker__Exante__ApiSecret=your-secret
AppSettings__Broker__Exante__AccountId=your-account-id

# Database
AppSettings__Database__ConnectionString=Data Source=/app/data/trading.db
```

### Krok 2: Inicjalizacja Consul

Przed pierwszym uruchomieniem zainicjuj konfigurację w Consul KV:

```bash
# Windows PowerShell
.\scripts\consul-load-config.ps1

# Linux/Mac
./scripts/consul-load-config.sh
```

> **Uwaga**: Skrypt wymaga pliku `.env` z credentialami. Skopiuj `.env.example` i dostosuj wartości przed uruchomieniem.

### Krok 3: Uruchomienie aplikacji

```bash
cd docker
docker-compose up -d
```

Serwisy startują w kolejności:
1. **Consul** (port 8500) - Service Discovery & Configuration
2. **TradingService** - Worker z codziennym skanerem (04:00 CET)
3. **TradingService.Api** - REST API + Ocelot Gateway (port 5001)
4. **Frontend** - Vue.js dashboard (port 5173)
5. **Grafana Loki** (opcjonalnie, port 3100) - Agregacja logów

### Krok 4: Weryfikacja

Sprawdź czy wszystkie serwisy działają:

```bash
docker-compose ps
```

Otwórz w przeglądarce:
- **Dashboard**: http://localhost:5173
- **API Swagger**: http://localhost:5001/swagger
- **Consul UI**: http://localhost:8500
- **Health Check**: http://localhost:5001/health

### Logi

Sprawdzanie logów:

```bash
# Wszystkie serwisy
docker-compose logs -f

# Konkretny serwis
docker-compose logs -f trading-service
docker-compose logs -f trading-api
docker-compose logs -f frontend
```

### Zatrzymanie i restart

```bash
# Zatrzymanie
docker-compose down

# Zatrzymanie z usunięciem wolumenów (baza danych, logi)
docker-compose down -v

# Restart konkretnego serwisu
docker-compose restart trading-service
```

## Konfiguracja

### Watchlist

Edytuj listę instrumentów do skanowania w `src/TradingService/appsettings.json`:

```json
{
  "AppSettings": {
    "Watchlist": ["SPY", "QQQ", "AAPL", "MSFT", "GOOGL", "NVDA", "AMD"],
    "ScanTime": "04:00"
  }
}
```

Lub ustaw przez Consul KV: `http://localhost:8500/ui/dc1/kv/TradingService/config`

### Strategia

Parametry strategii Short-Term PUT (2-3 tygodnie):

```json
{
  "Strategy": {
    "MinExpiryDays": 14,
    "MaxExpiryDays": 21,
    "MinConfidence": 0.6,
    "MinOTMPercent": 0.05,
    "MaxOTMPercent": 0.15
  }
}
```

## Rozwój lokalny

Bez Dockera:

```bash
# Backend
dotnet restore
dotnet build
dotnet run --project src/TradingService
dotnet run --project src/TradingService.Api

# Frontend
cd frontend
npm install
npm run dev
```

Wymaga lokalnego Consul: http://localhost:8500

## Licencja

GNU GENERAL PUBLIC LICENSE Version 3, 29 June 2007
**LICENSE FILE**: LICENSE.txt

