# PLAN.md - Plan Implementacji Aplikacji Strategii Opcji PUT

## Spis Treści

1. [Przegląd Projektu](#przegląd-projektu)
2. [Architektura i Struktura Katalogów](#architektura-i-struktura-katalogów)
3. [Faza 1: Fundament Projektu](#faza-1-fundament-projektu)
4. [Faza 2: Warstwa Danych](#faza-2-warstwa-danych)
5. [Faza 3: Integracje Zewnętrzne](#faza-3-integracje-zewnętrzne)
6. [Faza 4: Silnik Strategii](#faza-4-silnik-strategii)
7. [Faza 5: API z Ocelot Gateway i Service Discovery](#faza-5-api-z-ocelot-gateway-i-service-discovery)
8. [Faza 6: Integracja z Brokerem](#faza-6-integracja-z-brokerem)
9. [Faza 7: Frontend Vue.js](#faza-7-frontend-vuejs)
10. [Faza 8: Konteneryzacja i Deployment](#faza-8-konteneryzacja-i-deployment)
11. [Faza 9: Testy i Dokumentacja](#faza-9-testy-i-dokumentacja)

---

## Przegląd Projektu

### Cel
Automatyzacja wyszukiwania okazji do sprzedaży opcji PUT (short put) na instrumentach z silnym trendem wzrostowym, minimalizując ryzyko wykonania (ITM). Skupiamy się na opcjach z datą zapadalności 2-3 tygodnie.

### Stack Technologiczny

| Warstwa | Technologia | Wersja |
|---------|-------------|--------|
| Backend Runtime | .NET 10 (Preview/RC) | 10.0 |
| Baza Danych | SQLite + Linq2DB | 5.4.1 |
| Service Discovery | HashiCorp Consul | 1.7.14.9 |
| API Gateway | Ocelot (zintegrowany z API) | 24.0.1 |
| Logowanie | NLog + Grafana Loki | 6.0.7 / 2.2.1 |
| Frontend | Vue.js 3 + Vite + Tailwind CSS | 3.x |
| Konteneryzacja | Docker + Docker Compose | - |
| Dane Rynkowe | Yahoo Finance API, Exante API | 7.0.5 / 1.1.0 |

---

## Architektura i Struktura Katalogów

> **UWAGA:** Ocelot Gateway został zintegrowany bezpośrednio z TradingService.Api, eliminując potrzebę osobnego projektu Gateway. Zmniejsza to liczbę usług do zarządzania.

```
ToperStockExpertAdvisor/
├── src/
│   ├── TradingService/                    # Główny Worker Service
│   │   ├── TradingService.csproj
│   │   ├── Program.cs
│   │   ├── Worker.cs                      # BackgroundService z timerem
│   │   ├── appsettings.json
│   │   ├── nlog.config                    # Konfiguracja NLog
│   │   │
│   │   ├── Configuration/
│   │   │   ├── ConsulConfigProvider.cs    # Ładowanie konfiguracji z Consul KV
│   │   │   ├── AppSettings.cs             # POCO dla ustawień
│   │   │   └── BrokerConfig.cs            # Konfiguracja brokera
│   │   │
│   │   ├── Data/
│   │   │   ├── TradingDbContext.cs        # Linq2DB DataConnection
│   │   │   ├── Migrations/                # Migracje schematu
│   │   │   └── Entities/
│   │   │       ├── PutRecommendation.cs
│   │   │       ├── ScanLog.cs
│   │   │       └── WatchlistItem.cs
│   │   │
│   │   ├── Services/
│   │   │   ├── Interfaces/
│   │   │   │   ├── IMarketDataProvider.cs
│   │   │   │   ├── IStrategy.cs
│   │   │   │   ├── IBroker.cs
│   │   │   │   └── IBrokerFactory.cs
│   │   │   │
│   │   │   ├── MarketData/
│   │   │   │   ├── YahooFinanceProvider.cs
│   │   │   │   ├── ExanteDataProvider.cs
│   │   │   │   └── MarketDataAggregator.cs
│   │   │   │
│   │   │   ├── Brokers/
│   │   │   │   ├── BrokerFactory.cs
│   │   │   │   ├── ExanteBroker.cs
│   │   │   │   └── OrderExecutor.cs
│   │   │   │
│   │   │   └── DailyScanService.cs        # Orkiestracja skanowania
│   │   │
│   │   ├── Strategies/                     # Implementacje strategii
│   │   │   ├── StrategyLoader.cs          # Dynamiczne ładowanie via Reflection
│   │   │   ├── ShortTermPutStrategy.cs    # Strategia 2-3 tygodniowych opcji PUT
│   │   │   ├── DividendMomentumStrategy.cs
│   │   │   └── VolatilityCrushStrategy.cs
│   │   │
│   │   └── Models/
│   │       ├── MarketData.cs
│   │       ├── OptionsChain.cs
│   │       └── PutRecommendationDto.cs
│   │
│   ├── TradingService.Api/                # REST API + Ocelot Gateway (połączone)
│   │   ├── TradingService.Api.csproj
│   │   ├── Program.cs
│   │   ├── ocelot.json                    # Konfiguracja Ocelot (routing)
│   │   ├── nlog.config                    # Konfiguracja NLog dla API
│   │   ├── Controllers/
│   │   │   ├── RecommendationsController.cs
│   │   │   ├── StrategiesController.cs
│   │   │   └── HealthController.cs
│   │   └── Middleware/
│   │       └── ConsulRegistrationMiddleware.cs
│   │
│   └── Shared/                            # Wspólne komponenty
│       ├── Shared.csproj
│       ├── Constants/
│       │   └── ConsulPaths.cs
│       ├── Logging/
│       │   └── NLogConfiguration.cs       # Wspólna konfiguracja NLog
│       └── Extensions/
│           ├── ServiceCollectionExtensions.cs
│           └── ConsulExtensions.cs
│
├── frontend/                              # Vue.js Dashboard
│   ├── package.json
│   ├── vite.config.ts
│   ├── tailwind.config.js
│   ├── src/
│   │   ├── main.ts
│   │   ├── App.vue
│   │   ├── api/
│   │   │   └── recommendations.ts
│   │   ├── components/
│   │   │   ├── RecommendationsTable.vue
│   │   │   ├── ProfitCalculator.vue
│   │   │   ├── PriceChart.vue
│   │   │   └── StrategySelector.vue
│   │   ├── views/
│   │   │   ├── DashboardView.vue
│   │   │   └── SettingsView.vue
│   │   ├── stores/
│   │   │   └── recommendations.ts         # Pinia store
│   │   └── types/
│   │       └── index.ts
│   └── public/
│
├── tests/
│   ├── TradingService.Tests/
│   │   ├── Strategies/
│   │   ├── Services/
│   │   └── Integration/
│   └── TradingService.Api.Tests/
│
├── docker/
│   ├── docker-compose.yml
│   ├── docker-compose.override.yml
│   ├── Dockerfile.TradingService
│   ├── Dockerfile.Api                     # API + Gateway w jednym
│   └── Dockerfile.Frontend
│
├── scripts/
│   ├── consul-init.ps1                    # Inicjalizacja Consul KV
│   ├── setup-dev.ps1                      # Setup środowiska dev
│   └── run-local.ps1                      # Uruchomienie lokalne
│
├── docs/
│   ├── API.md
│   ├── STRATEGIES.md
│   └── DEPLOYMENT.md
│
├── .claude/
│   └── PRD.md
│
├── ToperStockExpertAdvisor.sln
├── PLAN.md
├── README.md
├── .gitignore
└── .editorconfig
```

---

## Faza 1: Fundament Projektu

### 1.1 Inicjalizacja Solution i Projektów

**Zadania:**

1. **Utworzenie struktury solution**
   ```powershell
   dotnet new sln -n ToperStockExpertAdvisor
   dotnet new worker -n TradingService -o src/TradingService
   dotnet new webapi -n TradingService.Api -o src/TradingService.Api
   dotnet new classlib -n Shared -o src/Shared
   ```

2. **Dodanie projektów do solution**
   ```powershell
   dotnet sln add src/TradingService/TradingService.csproj
   dotnet sln add src/TradingService.Api/TradingService.Api.csproj
   dotnet sln add src/Shared/Shared.csproj
   ```

3. **Konfiguracja referencji między projektami**
   - TradingService → Shared
   - TradingService.Api → Shared, TradingService (Models/Entities)

4. **Utworzenie plików konfiguracyjnych**
   - `.gitignore` (Visual Studio + Node.js)
   - `.editorconfig` (formatowanie kodu)
   - `global.json` (wersja .NET SDK)

### 1.2 Instalacja Pakietów NuGet

**TradingService:**
```xml
<!-- Consul -->
<PackageReference Include="Consul" Version="1.7.14.9" />
<PackageReference Include="Consul.AspNetCore" Version="1.7.14.9" />

<!-- Baza danych -->
<PackageReference Include="linq2db.SQLite" Version="5.4.1" />

<!-- Dane rynkowe -->
<PackageReference Include="YahooQuotesApi" Version="7.0.5" />
<PackageReference Include="Exante.Net" Version="1.1.0" />

<!-- Hosting -->
<PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.0" />

<!-- Logowanie NLog -->
<PackageReference Include="NLog" Version="6.0.7" />
<PackageReference Include="NLog.Extensions.Logging" Version="6.1.0" />
<PackageReference Include="NLog.Targets.Loki" Version="2.2.1" />
```

**TradingService.Api (z Ocelot Gateway):**
```xml
<!-- Consul -->
<PackageReference Include="Consul" Version="1.7.14.9" />
<PackageReference Include="Consul.AspNetCore" Version="1.7.14.9" />

<!-- Ocelot Gateway (zintegrowany) -->
<PackageReference Include="Ocelot" Version="24.0.1" />
<PackageReference Include="Ocelot.Provider.Consul" Version="24.0.0" />

<!-- Swagger -->
<PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />

<!-- Logowanie NLog -->
<PackageReference Include="NLog" Version="6.0.7" />
<PackageReference Include="NLog.Extensions.Logging" Version="6.1.0" />
<PackageReference Include="NLog.Web.AspNetCore" Version="6.1.0" />
<PackageReference Include="NLog.Targets.Loki" Version="2.2.1" />
```

**Shared:**
```xml
<PackageReference Include="NLog" Version="6.0.7" />
<PackageReference Include="NLog.Extensions.Logging" Version="6.1.0" />
```

### 1.3 Konfiguracja NLog z Grafana Loki

**Zadania:**
1. Konfiguracja NLog dla logowania do pliku i Grafana Loki
2. Implementacja `Program.cs` z konfiguracją hosta
3. Implementacja bazowego `Worker.cs` z timerem
4. Utworzenie `appsettings.json` i `appsettings.Development.json`

**nlog.config:**
```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
      autoReload="true"
      throwConfigExceptions="true">

  <extensions>
    <add assembly="NLog.Targets.Loki" />
  </extensions>

  <variable name="appName" value="TradingService" />

  <targets async="true">
    <!-- Plik logów -->
    <target xsi:type="File"
            name="fileTarget"
            fileName="${basedir}/logs/${appName}-${shortdate}.log"
            layout="${longdate}|${level:uppercase=true}|${logger}|${message}|${exception:format=tostring}"
            archiveEvery="Day"
            archiveNumbering="Rolling"
            maxArchiveFiles="30" />

    <!-- Konsola -->
    <target xsi:type="Console"
            name="consoleTarget"
            layout="${longdate}|${level:uppercase=true}|${logger:shortName=true}|${message}" />

    <!-- Grafana Loki -->
    <target xsi:type="Loki"
            name="lokiTarget"
            endpoint="${configsetting:item=Loki.Endpoint}"
            username="${configsetting:item=Loki.Username}"
            password="${configsetting:item=Loki.Password}"
            orderWrites="true"
            compressionLevel="noCompression"
            layout="${longdate}|${level:uppercase=true}|${logger}|${message}|${exception:format=tostring}">
      <label name="app" layout="${appName}" />
      <label name="environment" layout="${configsetting:item=Environment}" />
      <label name="level" layout="${level:lowercase=true}" />
      <label name="host" layout="${hostname}" />
    </target>
  </targets>

  <rules>
    <!-- Wszystkie logi do pliku -->
    <logger name="*" minlevel="Info" writeTo="fileTarget" />

    <!-- Info i wyższe do konsoli -->
    <logger name="*" minlevel="Info" writeTo="consoleTarget" />

    <!-- Warning i wyższe do Loki -->
    <logger name="*" minlevel="Warn" writeTo="lokiTarget" />

    <!-- Logi aplikacji (Debug+) do Loki -->
    <logger name="TradingService.*" minlevel="Debug" writeTo="lokiTarget" />
  </rules>
</nlog>
```

**appsettings.json (sekcja Loki):**
```json
{
  "Loki": {
    "Endpoint": "http://localhost:3100",
    "Username": "",
    "Password": ""
  },
  "Environment": "Development"
}
```

**Program.cs z NLog:**
```csharp
using NLog;
using NLog.Extensions.Logging;

var logger = LogManager.Setup()
    .LoadConfigurationFromFile("nlog.config")
    .GetCurrentClassLogger();

try
{
    logger.Info("Starting TradingService...");

    var builder = Host.CreateApplicationBuilder(args);

    // Konfiguracja NLog
    builder.Logging.ClearProviders();
    builder.Logging.AddNLog();

    builder.Services.AddHostedService<Worker>();
    // ... pozostałe serwisy

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    logger.Error(ex, "Application stopped due to exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}
```

**Przykład Worker.cs:**
```csharp
public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceProvider _services;

    public Worker(ILogger<Worker> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var scheduledTime = GetNextScanTime(); // np. 04:00 CET
            var delay = scheduledTime - now;

            if (delay > TimeSpan.Zero)
            {
                _logger.LogInformation("Next scan scheduled at {ScheduledTime}", scheduledTime);
                await Task.Delay(delay, stoppingToken);
            }

            using var scope = _services.CreateScope();
            var scanService = scope.ServiceProvider.GetRequiredService<IDailyScanService>();

            _logger.LogInformation("Starting daily scan...");
            await scanService.ExecuteScanAsync(stoppingToken);
            _logger.LogInformation("Daily scan completed");
        }
    }

    private DateTime GetNextScanTime()
    {
        var today = DateTime.Today;
        var scanTime = today.AddHours(4); // 04:00

        if (DateTime.Now > scanTime)
            scanTime = scanTime.AddDays(1);

        return scanTime;
    }
}
```

---

## Faza 2: Warstwa Danych

### 2.1 Konfiguracja Linq2DB z SQLite

**Zadania:**

1. **Utworzenie encji bazy danych**

   ```csharp
   // PutRecommendation.cs
   [Table("Recommendations")]
   public class PutRecommendation
   {
       [PrimaryKey, Identity]
       public int Id { get; set; }

       [Column, NotNull]
       public string Symbol { get; set; } = string.Empty;

       [Column]
       public decimal CurrentPrice { get; set; }

       [Column]
       public decimal StrikePrice { get; set; }

       [Column]
       public DateTime Expiry { get; set; }

       [Column]
       public int DaysToExpiry { get; set; }  // 14-21 dni dla strategii short-term

       [Column]
       public decimal Premium { get; set; }

       [Column]
       public decimal Breakeven { get; set; }

       [Column]
       public decimal Confidence { get; set; }

       [Column]
       public decimal ExpectedGrowthPercent { get; set; }  // Oczekiwany wzrost w %

       [Column]
       public string StrategyName { get; set; } = string.Empty;

       [Column]
       public DateTime ScannedAt { get; set; }

       [Column]
       public bool IsActive { get; set; } = true;
   }
   ```

2. **Utworzenie ScanLog dla historii skanów**
   ```csharp
   [Table("ScanLogs")]
   public class ScanLog
   {
       [PrimaryKey, Identity]
       public int Id { get; set; }

       [Column]
       public DateTime StartedAt { get; set; }

       [Column]
       public DateTime? CompletedAt { get; set; }

       [Column]
       public int SymbolsScanned { get; set; }

       [Column]
       public int RecommendationsGenerated { get; set; }

       [Column]
       public string? ErrorMessage { get; set; }

       [Column]
       public string Status { get; set; } = "Running";
   }
   ```

3. **Implementacja TradingDbContext**
   ```csharp
   public class TradingDbContext : DataConnection
   {
       public TradingDbContext(DataOptions<TradingDbContext> options)
           : base(options.Options) { }

       public ITable<PutRecommendation> Recommendations => this.GetTable<PutRecommendation>();
       public ITable<ScanLog> ScanLogs => this.GetTable<ScanLog>();
       public ITable<WatchlistItem> Watchlist => this.GetTable<WatchlistItem>();
   }
   ```

4. **Utworzenie skryptu migracji/inicjalizacji bazy**
   ```csharp
   public static class DatabaseInitializer
   {
       public static async Task InitializeAsync(TradingDbContext db)
       {
           await db.CreateTableAsync<PutRecommendation>(tableOptions: TableOptions.CreateIfNotExists);
           await db.CreateTableAsync<ScanLog>(tableOptions: TableOptions.CreateIfNotExists);
           await db.CreateTableAsync<WatchlistItem>(tableOptions: TableOptions.CreateIfNotExists);
       }
   }
   ```

### 2.2 Repozytorium Danych

**Zadania:**

1. **Interfejs IRecommendationRepository**
   ```csharp
   public interface IRecommendationRepository
   {
       Task<IEnumerable<PutRecommendation>> GetActiveRecommendationsAsync();
       Task<IEnumerable<PutRecommendation>> GetBySymbolAsync(string symbol);
       Task<IEnumerable<PutRecommendation>> GetShortTermRecommendationsAsync(int minDays = 14, int maxDays = 21);
       Task<int> AddRangeAsync(IEnumerable<PutRecommendation> recommendations);
       Task DeactivateOldRecommendationsAsync(DateTime before);
   }
   ```

2. **Implementacja z Linq2DB**
   ```csharp
   public class RecommendationRepository : IRecommendationRepository
   {
       private readonly TradingDbContext _db;
       private readonly ILogger<RecommendationRepository> _logger;

       public async Task<IEnumerable<PutRecommendation>> GetActiveRecommendationsAsync()
       {
           return await _db.Recommendations
               .Where(r => r.IsActive && r.Expiry > DateTime.UtcNow)
               .OrderByDescending(r => r.Confidence)
               .ToListAsync();
       }

       public async Task<IEnumerable<PutRecommendation>> GetShortTermRecommendationsAsync(
           int minDays = 14, int maxDays = 21)
       {
           return await _db.Recommendations
               .Where(r => r.IsActive
                        && r.DaysToExpiry >= minDays
                        && r.DaysToExpiry <= maxDays
                        && r.Expiry > DateTime.UtcNow)
               .OrderByDescending(r => r.Confidence)
               .ToListAsync();
       }
   }
   ```

---

## Faza 3: Integracje Zewnętrzne

### 3.1 Yahoo Finance Provider (YahooQuotesApi)

**Zadania:**

1. **Interfejs IMarketDataProvider**
   ```csharp
   public interface IMarketDataProvider
   {
       Task<MarketData?> GetMarketDataAsync(string symbol);
       Task<IEnumerable<HistoricalQuote>> GetHistoricalDataAsync(string symbol, int days);
       Task<DividendInfo?> GetDividendInfoAsync(string symbol);
       Task<TrendAnalysis> AnalyzeTrendAsync(string symbol, int days = 21);  // Analiza trendu 2-3 tyg.
   }
   ```

2. **Model MarketData z analizą trendu**
   ```csharp
   public record MarketData
   {
       public string Symbol { get; init; } = string.Empty;
       public decimal CurrentPrice { get; init; }
       public decimal Open { get; init; }
       public decimal High { get; init; }
       public decimal Low { get; init; }
       public decimal Close { get; init; }
       public long Volume { get; init; }
       public decimal AverageVolume { get; init; }
       public decimal High52Week { get; init; }
       public decimal Low52Week { get; init; }
       public decimal MovingAverage50 { get; init; }
       public decimal MovingAverage200 { get; init; }
       public decimal MovingAverage20 { get; init; }  // Krótkoterminowa MA
       public decimal RSI { get; init; }
       public decimal MACD { get; init; }
       public decimal MACDSignal { get; init; }
       public DateTime Timestamp { get; init; }
   }

   public record TrendAnalysis
   {
       public string Symbol { get; init; } = string.Empty;
       public decimal ExpectedGrowthPercent { get; init; }  // Prognozowany wzrost w %
       public decimal TrendStrength { get; init; }          // Siła trendu 0-1
       public TrendDirection Direction { get; init; }
       public decimal Confidence { get; init; }
       public int AnalysisPeriodDays { get; init; }
   }

   public enum TrendDirection { Up, Down, Sideways }
   ```

3. **Implementacja YahooFinanceProvider z YahooQuotesApi**
   ```csharp
   public class YahooFinanceProvider : IMarketDataProvider
   {
       private readonly ILogger<YahooFinanceProvider> _logger;
       private readonly YahooQuotes _yahooQuotes;

       public YahooFinanceProvider(ILogger<YahooFinanceProvider> logger)
       {
           _logger = logger;
           _yahooQuotes = new YahooQuotesBuilder().Build();
       }

       public async Task<MarketData?> GetMarketDataAsync(string symbol)
       {
           try
           {
               var security = await _yahooQuotes.GetAsync(symbol, Histories.PriceHistory);

               if (security == null)
                   return null;

               var history = security.PriceHistory.Value;
               var prices = history.Select(h => (decimal)h.Close).ToList();

               return new MarketData
               {
                   Symbol = symbol,
                   CurrentPrice = (decimal)security.RegularMarketPrice,
                   Open = (decimal)(security.RegularMarketOpen ?? 0),
                   High = (decimal)(security.RegularMarketDayHigh ?? 0),
                   Low = (decimal)(security.RegularMarketDayLow ?? 0),
                   Close = (decimal)security.RegularMarketPrice,
                   Volume = security.RegularMarketVolume ?? 0,
                   AverageVolume = security.AverageDailyVolume10Day ?? 0,
                   High52Week = (decimal)(security.FiftyTwoWeekHigh ?? 0),
                   Low52Week = (decimal)(security.FiftyTwoWeekLow ?? 0),
                   MovingAverage50 = (decimal)(security.FiftyDayAverage ?? 0),
                   MovingAverage200 = (decimal)(security.TwoHundredDayAverage ?? 0),
                   MovingAverage20 = TechnicalIndicators.CalculateMovingAverage(prices, 20),
                   RSI = TechnicalIndicators.CalculateRSI(prices, 14),
                   Timestamp = DateTime.UtcNow
               };
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Failed to fetch data for {Symbol}", symbol);
               return null;
           }
       }

       public async Task<TrendAnalysis> AnalyzeTrendAsync(string symbol, int days = 21)
       {
           var history = await GetHistoricalDataAsync(symbol, days + 10);
           var prices = history.Select(h => h.Close).ToList();

           // Analiza trendu dla okresu 2-3 tygodni
           var recentPrices = prices.TakeLast(days).ToList();
           var startPrice = recentPrices.First();
           var endPrice = recentPrices.Last();
           var growthPercent = ((endPrice - startPrice) / startPrice) * 100;

           // Siła trendu na podstawie regresji liniowej
           var trendStrength = CalculateTrendStrength(recentPrices);

           // Kierunek
           var direction = growthPercent > 1 ? TrendDirection.Up
                         : growthPercent < -1 ? TrendDirection.Down
                         : TrendDirection.Sideways;

           // Prognoza na kolejne 2-3 tygodnie
           var expectedGrowth = PredictGrowth(recentPrices, days);

           return new TrendAnalysis
           {
               Symbol = symbol,
               ExpectedGrowthPercent = expectedGrowth,
               TrendStrength = trendStrength,
               Direction = direction,
               Confidence = CalculateConfidence(trendStrength, direction),
               AnalysisPeriodDays = days
           };
       }
   }
   ```

4. **Kalkulacja wskaźników technicznych**
   ```csharp
   public static class TechnicalIndicators
   {
       public static decimal CalculateRSI(IEnumerable<decimal> prices, int period = 14)
       {
           var priceList = prices.ToList();
           if (priceList.Count < period + 1)
               return 50m;

           var gains = new List<decimal>();
           var losses = new List<decimal>();

           for (int i = 1; i < priceList.Count; i++)
           {
               var change = priceList[i] - priceList[i - 1];
               gains.Add(change > 0 ? change : 0);
               losses.Add(change < 0 ? -change : 0);
           }

           var avgGain = gains.TakeLast(period).Average();
           var avgLoss = losses.TakeLast(period).Average();

           if (avgLoss == 0)
               return 100m;

           var rs = avgGain / avgLoss;
           return 100m - (100m / (1m + rs));
       }

       public static decimal CalculateMovingAverage(IEnumerable<decimal> prices, int period)
       {
           var priceList = prices.ToList();
           if (priceList.Count < period)
               return priceList.Average();

           return priceList.TakeLast(period).Average();
       }

       public static (decimal macd, decimal signal) CalculateMACD(
           IEnumerable<decimal> prices,
           int fastPeriod = 12,
           int slowPeriod = 26,
           int signalPeriod = 9)
       {
           var priceList = prices.ToList();

           var emaFast = CalculateEMA(priceList, fastPeriod);
           var emaSlow = CalculateEMA(priceList, slowPeriod);
           var macd = emaFast - emaSlow;

           // Simplified signal calculation
           var signal = macd * 0.9m;

           return (macd, signal);
       }

       private static decimal CalculateEMA(List<decimal> prices, int period)
       {
           if (prices.Count < period)
               return prices.Average();

           var multiplier = 2m / (period + 1);
           var ema = prices.Take(period).Average();

           foreach (var price in prices.Skip(period))
           {
               ema = (price - ema) * multiplier + ema;
           }

           return ema;
       }
   }
   ```

### 3.2 Exante Data Provider

**Zadania:**

1. **Model OptionsChain**
   ```csharp
   public record OptionsChain
   {
       public string UnderlyingSymbol { get; init; } = string.Empty;
       public IReadOnlyList<OptionContract> PutOptions { get; init; } = [];
       public IReadOnlyList<OptionContract> CallOptions { get; init; } = [];
   }

   public record OptionContract
   {
       public string Symbol { get; init; } = string.Empty;
       public decimal Strike { get; init; }
       public DateTime Expiry { get; init; }
       public int DaysToExpiry => (int)(Expiry - DateTime.Today).TotalDays;
       public decimal Bid { get; init; }
       public decimal Ask { get; init; }
       public decimal Mid => (Bid + Ask) / 2;
       public decimal ImpliedVolatility { get; init; }
       public decimal OpenInterest { get; init; }
       public decimal Delta { get; init; }
       public decimal Theta { get; init; }
   }
   ```

2. **Implementacja ExanteDataProvider**
   ```csharp
   public class ExanteDataProvider : IOptionsDataProvider
   {
       private readonly ExanteClient _client;
       private readonly ILogger<ExanteDataProvider> _logger;

       public ExanteDataProvider(BrokerConfig config, ILogger<ExanteDataProvider> logger)
       {
           _client = new ExanteClient(new ExanteClientOptions
           {
               ApiKey = config.ApiKey,
               ApiSecret = config.Secret,
               AccountId = config.AccountId
           });
           _logger = logger;
       }

       public async Task<OptionsChain?> GetOptionsChainAsync(string symbol)
       {
           try
           {
               // Pobieranie opcji chain z Exante API
               var options = await _client.GetOptionsAsync(symbol);

               return new OptionsChain
               {
                   UnderlyingSymbol = symbol,
                   PutOptions = options
                       .Where(o => o.OptionType == "PUT")
                       .Select(MapToOptionContract)
                       .ToList(),
                   CallOptions = options
                       .Where(o => o.OptionType == "CALL")
                       .Select(MapToOptionContract)
                       .ToList()
               };
           }
           catch (Exception ex)
           {
               _logger.LogError(ex, "Failed to fetch options chain for {Symbol}", symbol);
               return null;
           }
       }

       /// <summary>
       /// Pobiera opcje PUT z datą zapadalności 14-21 dni
       /// </summary>
       public async Task<IEnumerable<OptionContract>> GetShortTermPutOptionsAsync(
           string symbol,
           int minDays = 14,
           int maxDays = 21)
       {
           var chain = await GetOptionsChainAsync(symbol);

           if (chain == null)
               return Enumerable.Empty<OptionContract>();

           return chain.PutOptions
               .Where(p => p.DaysToExpiry >= minDays && p.DaysToExpiry <= maxDays)
               .OrderBy(p => p.Expiry)
               .ThenByDescending(p => p.Strike);
       }
   }
   ```

### 3.3 Market Data Aggregator

**Zadania:**

1. **Agregacja danych z wielu źródeł**
   ```csharp
   public class MarketDataAggregator : IMarketDataAggregator
   {
       private readonly IMarketDataProvider _yahooProvider;
       private readonly IOptionsDataProvider _exanteProvider;
       private readonly ILogger<MarketDataAggregator> _logger;

       public async Task<AggregatedMarketData> GetFullMarketDataAsync(string symbol)
       {
           _logger.LogDebug("Fetching aggregated data for {Symbol}", symbol);

           var marketDataTask = _yahooProvider.GetMarketDataAsync(symbol);
           var trendTask = _yahooProvider.AnalyzeTrendAsync(symbol, 21);  // 3-tygodniowa analiza
           var optionsTask = _exanteProvider.GetShortTermPutOptionsAsync(symbol, 14, 21);
           var dividendTask = _yahooProvider.GetDividendInfoAsync(symbol);

           await Task.WhenAll(marketDataTask, trendTask, optionsTask, dividendTask);

           return new AggregatedMarketData
           {
               MarketData = await marketDataTask,
               TrendAnalysis = await trendTask,
               ShortTermPutOptions = (await optionsTask).ToList(),
               DividendInfo = await dividendTask
           };
       }
   }

   public record AggregatedMarketData
   {
       public MarketData? MarketData { get; init; }
       public TrendAnalysis? TrendAnalysis { get; init; }
       public IReadOnlyList<OptionContract> ShortTermPutOptions { get; init; } = [];
       public DividendInfo? DividendInfo { get; init; }
   }
   ```

---

## Faza 4: Silnik Strategii

### 4.1 Interfejs i Bazowa Klasa Strategii

**Zadania:**

1. **Interfejs IStrategy**
   ```csharp
   public interface IStrategy
   {
       string Name { get; }
       string Description { get; }
       int TargetExpiryMinDays { get; }  // Minimalny czas do wygaśnięcia
       int TargetExpiryMaxDays { get; }  // Maksymalny czas do wygaśnięcia
       Task<IEnumerable<PutRecommendation>> AnalyzeAsync(
           AggregatedMarketData data,
           CancellationToken cancellationToken = default);
   }
   ```

2. **Bazowa klasa abstrakcyjna**
   ```csharp
   public abstract class StrategyBase : IStrategy
   {
       public abstract string Name { get; }
       public abstract string Description { get; }
       public virtual int TargetExpiryMinDays => 14;  // Domyślnie 2 tygodnie
       public virtual int TargetExpiryMaxDays => 21;  // Domyślnie 3 tygodnie

       protected readonly ILogger Logger;

       protected StrategyBase(ILogger logger)
       {
           Logger = logger;
       }

       public abstract Task<IEnumerable<PutRecommendation>> AnalyzeAsync(
           AggregatedMarketData data,
           CancellationToken cancellationToken = default);

       protected PutRecommendation CreateRecommendation(
           string symbol,
           OptionContract option,
           decimal confidence,
           decimal currentPrice,
           decimal expectedGrowthPercent)
       {
           return new PutRecommendation
           {
               Symbol = symbol,
               CurrentPrice = currentPrice,
               StrikePrice = option.Strike,
               Expiry = option.Expiry,
               DaysToExpiry = option.DaysToExpiry,
               Premium = option.Mid,
               Breakeven = option.Strike - option.Mid,
               Confidence = confidence,
               ExpectedGrowthPercent = expectedGrowthPercent,
               StrategyName = Name,
               ScannedAt = DateTime.UtcNow
           };
       }

       protected IEnumerable<OptionContract> FilterByExpiry(
           IEnumerable<OptionContract> options)
       {
           return options.Where(o =>
               o.DaysToExpiry >= TargetExpiryMinDays &&
               o.DaysToExpiry <= TargetExpiryMaxDays);
       }
   }
   ```

### 4.2 Implementacja Strategii

**Strategia 1: Short-Term PUT (2-3 tygodnie) - Główna strategia**
```csharp
/// <summary>
/// Strategia sprzedaży opcji PUT na instrumenty z prognozowanym wzrostem w okresie 2-3 tygodni.
/// Wybiera opcje z datą zapadalności 14-21 dni, gdzie instrument ma wysokie prawdopodobieństwo wzrostu.
/// </summary>
public class ShortTermPutStrategy : StrategyBase
{
    public override string Name => "Short-Term PUT (2-3 weeks)";
    public override string Description =>
        "Sprzedaż opcji PUT na instrumenty z prognozowanym wzrostem w ciągu 2-3 tygodni. " +
        "Opcje z datą zapadalności 14-21 dni.";

    public override int TargetExpiryMinDays => 14;
    public override int TargetExpiryMaxDays => 21;

    public ShortTermPutStrategy(ILogger<ShortTermPutStrategy> logger) : base(logger) { }

    public override async Task<IEnumerable<PutRecommendation>> AnalyzeAsync(
        AggregatedMarketData data,
        CancellationToken cancellationToken = default)
    {
        var recommendations = new List<PutRecommendation>();
        var market = data.MarketData;
        var trend = data.TrendAnalysis;

        if (market == null || trend == null || !data.ShortTermPutOptions.Any())
        {
            Logger.LogDebug("Insufficient data for {Symbol}", market?.Symbol);
            return recommendations;
        }

        // Kryteria dla strategii 2-3 tygodniowej:
        // 1. Trend wzrostowy (prognozowany wzrost > 2%)
        // 2. RSI w strefie 40-70 (nie przekupiony, nie przesprzedany)
        // 3. Cena powyżej 20-dniowej MA
        // 4. Siła trendu > 0.5
        // 5. MACD powyżej linii sygnału

        bool strongUptrend = trend.ExpectedGrowthPercent > 2m;
        bool healthyRSI = market.RSI >= 40 && market.RSI <= 70;
        bool aboveShortMA = market.CurrentPrice > market.MovingAverage20;
        bool strongTrend = trend.TrendStrength > 0.5m;
        bool bullishMACD = market.MACD > market.MACDSignal;

        Logger.LogDebug(
            "Analysis for {Symbol}: Uptrend={Uptrend}, RSI={RSI}, AboveMA20={AboveMA}, " +
            "TrendStrength={Strength}, BullishMACD={MACD}",
            market.Symbol, strongUptrend, healthyRSI, aboveShortMA, strongTrend, bullishMACD);

        // Wymagamy minimum 3 z 5 kryteriów
        var criteriaMetCount = new[] { strongUptrend, healthyRSI, aboveShortMA, strongTrend, bullishMACD }
            .Count(c => c);

        if (criteriaMetCount < 3)
        {
            Logger.LogDebug("{Symbol} does not meet minimum criteria ({Count}/5)",
                market.Symbol, criteriaMetCount);
            return recommendations;
        }

        // Filtruj opcje PUT z odpowiednim terminem zapadalności (14-21 dni)
        var eligiblePuts = FilterByExpiry(data.ShortTermPutOptions)
            .Where(p => p.Strike < market.CurrentPrice * 0.95m)  // OTM minimum 5%
            .Where(p => p.Strike > market.CurrentPrice * 0.85m)  // Nie więcej niż 15% OTM
            .Where(p => p.Bid > 0.10m)  // Minimalna premia
            .OrderByDescending(p => p.Strike)  // Preferuj wyższe strike (więcej premii)
            .Take(3);

        foreach (var put in eligiblePuts)
        {
            var confidence = CalculateConfidence(market, trend, put, criteriaMetCount);

            if (confidence >= 0.6m)  // Minimum 60% pewności
            {
                var recommendation = CreateRecommendation(
                    market.Symbol,
                    put,
                    confidence,
                    market.CurrentPrice,
                    trend.ExpectedGrowthPercent);

                recommendations.Add(recommendation);

                Logger.LogInformation(
                    "Generated recommendation: {Symbol} PUT @ {Strike}, expiry {Expiry} ({Days}d), " +
                    "premium {Premium}, confidence {Confidence:P0}",
                    recommendation.Symbol, recommendation.StrikePrice,
                    recommendation.Expiry.ToShortDateString(), recommendation.DaysToExpiry,
                    recommendation.Premium, recommendation.Confidence);
            }
        }

        return recommendations;
    }

    private decimal CalculateConfidence(
        MarketData market,
        TrendAnalysis trend,
        OptionContract option,
        int criteriaMetCount)
    {
        decimal score = 0.4m;  // Bazowy score

        // Bonus za spełnione kryteria
        score += criteriaMetCount * 0.08m;

        // Bonus za silny trend wzrostowy
        if (trend.ExpectedGrowthPercent > 5m) score += 0.1m;
        else if (trend.ExpectedGrowthPercent > 3m) score += 0.05m;

        // Bonus za stabilny RSI (45-60 - idealna strefa)
        if (market.RSI >= 45 && market.RSI <= 60) score += 0.1m;

        // Bonus za niską zmienność implikowaną (niższe ryzyko)
        if (option.ImpliedVolatility < 0.25m) score += 0.1m;
        else if (option.ImpliedVolatility < 0.35m) score += 0.05m;

        // Bonus za głęboki OTM (większe bezpieczeństwo)
        var otmPercent = (market.CurrentPrice - option.Strike) / market.CurrentPrice;
        if (otmPercent > 0.10m) score += 0.05m;

        // Bonus za wysoką siłę trendu
        if (trend.TrendStrength > 0.7m) score += 0.1m;

        return Math.Min(score, 0.95m);  // Max 95%
    }
}
```

**Strategia 2: Dividend Momentum**
```csharp
public class DividendMomentumStrategy : StrategyBase
{
    public override string Name => "Dividend Momentum";
    public override string Description => "Szuka spółek dywidendowych z potencjałem wzrostu przed ex-date";

    public override int TargetExpiryMinDays => 14;
    public override int TargetExpiryMaxDays => 21;

    public DividendMomentumStrategy(ILogger<DividendMomentumStrategy> logger) : base(logger) { }

    public override async Task<IEnumerable<PutRecommendation>> AnalyzeAsync(
        AggregatedMarketData data,
        CancellationToken cancellationToken = default)
    {
        var recommendations = new List<PutRecommendation>();
        var market = data.MarketData;
        var dividend = data.DividendInfo;

        if (market == null || dividend == null || !data.ShortTermPutOptions.Any())
            return recommendations;

        // Kryteria: dividend yield > 3%, ex-date w ciągu 3-6 tygodni
        bool highYield = dividend.DividendYield > 0.03m;
        bool exDateUpcoming = dividend.ExDividendDate > DateTime.Now.AddDays(21)
                            && dividend.ExDividendDate < DateTime.Now.AddDays(45);
        bool priceNearSupport = market.CurrentPrice < market.MovingAverage50 * 1.05m;

        if (highYield && exDateUpcoming && priceNearSupport)
        {
            var eligiblePuts = FilterByExpiry(data.ShortTermPutOptions)
                .Where(p => p.Strike < market.CurrentPrice * 0.97m)
                .Take(2);

            foreach (var put in eligiblePuts)
            {
                var confidence = 0.65m + (dividend.DividendYield * 2m);
                var expectedGrowth = dividend.DividendYield * 100m * 0.5m; // Połowa yieldu jako wzrost

                recommendations.Add(CreateRecommendation(
                    market.Symbol, put, Math.Min(confidence, 0.90m),
                    market.CurrentPrice, expectedGrowth));
            }
        }

        return recommendations;
    }
}
```

**Strategia 3: Volatility Crush**
```csharp
public class VolatilityCrushStrategy : StrategyBase
{
    public override string Name => "Volatility Crush";
    public override string Description => "Wykorzystuje niską zmienność implikowaną dla bezpieczniejszych transakcji";

    public override int TargetExpiryMinDays => 14;
    public override int TargetExpiryMaxDays => 21;

    public VolatilityCrushStrategy(ILogger<VolatilityCrushStrategy> logger) : base(logger) { }

    public override async Task<IEnumerable<PutRecommendation>> AnalyzeAsync(
        AggregatedMarketData data,
        CancellationToken cancellationToken = default)
    {
        var recommendations = new List<PutRecommendation>();

        if (data.MarketData == null || !data.ShortTermPutOptions.Any())
            return recommendations;

        var market = data.MarketData;
        var trend = data.TrendAnalysis;

        // Kryteria: niska IV, stabilny trend
        var avgIV = data.ShortTermPutOptions.Average(p => p.ImpliedVolatility);
        bool lowIV = avgIV < 0.25m;
        bool stableUptrend = trend?.Direction == TrendDirection.Up && trend.TrendStrength > 0.4m;

        if (lowIV && stableUptrend)
        {
            var otmPuts = FilterByExpiry(data.ShortTermPutOptions)
                .Where(p => p.Strike < market.CurrentPrice * 0.93m)
                .Where(p => p.ImpliedVolatility < 0.3m)
                .OrderByDescending(p => p.Bid)
                .Take(2);

            foreach (var put in otmPuts)
            {
                var confidence = 0.6m + ((0.3m - avgIV) * 0.5m);
                recommendations.Add(CreateRecommendation(
                    market.Symbol, put, confidence, market.CurrentPrice,
                    trend?.ExpectedGrowthPercent ?? 0));
            }
        }

        return recommendations;
    }
}
```

### 4.3 Strategy Loader (Dynamiczne Ładowanie)

```csharp
public class StrategyLoader : IStrategyLoader
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<StrategyLoader> _logger;

    public IEnumerable<IStrategy> LoadAllStrategies()
    {
        var strategyInterface = typeof(IStrategy);
        var strategyTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => strategyInterface.IsAssignableFrom(t)
                     && !t.IsAbstract
                     && !t.IsInterface);

        foreach (var type in strategyTypes)
        {
            IStrategy? strategy = null;
            try
            {
                strategy = (IStrategy)ActivatorUtilities
                    .CreateInstance(_serviceProvider, type);

                _logger.LogInformation(
                    "Loaded strategy: {Name} (expiry: {Min}-{Max} days)",
                    strategy.Name, strategy.TargetExpiryMinDays, strategy.TargetExpiryMaxDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load strategy {Type}", type.Name);
            }

            if (strategy != null)
                yield return strategy;
        }
    }
}
```

### 4.4 Daily Scan Service

```csharp
public class DailyScanService : IDailyScanService
{
    private readonly IStrategyLoader _strategyLoader;
    private readonly IMarketDataAggregator _dataAggregator;
    private readonly IRecommendationRepository _repository;
    private readonly IConsulConfigProvider _configProvider;
    private readonly ILogger<DailyScanService> _logger;

    public async Task ExecuteScanAsync(CancellationToken cancellationToken)
    {
        var scanLog = await StartScanLogAsync();

        try
        {
            var config = await _configProvider.GetConfigAsync();
            var watchlist = config.Watchlist;
            var strategies = _strategyLoader.LoadAllStrategies().ToList();

            _logger.LogInformation(
                "Starting scan with {StrategyCount} strategies for {SymbolCount} symbols",
                strategies.Count, watchlist.Count);

            var allRecommendations = new List<PutRecommendation>();

            foreach (var symbol in watchlist)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _logger.LogDebug("Scanning {Symbol}...", symbol);
                var marketData = await _dataAggregator.GetFullMarketDataAsync(symbol);

                foreach (var strategy in strategies)
                {
                    var recommendations = await strategy.AnalyzeAsync(marketData, cancellationToken);
                    allRecommendations.AddRange(recommendations);
                }
            }

            // Dezaktywuj stare rekomendacje
            await _repository.DeactivateOldRecommendationsAsync(DateTime.UtcNow.AddDays(-1));

            // Zapisz nowe
            var count = await _repository.AddRangeAsync(allRecommendations);

            _logger.LogInformation(
                "Scan completed: {Count} recommendations generated from {Symbols} symbols",
                count, watchlist.Count);

            await CompleteScanLogAsync(scanLog, watchlist.Count, allRecommendations.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
            await FailScanLogAsync(scanLog, ex.Message);
            throw;
        }
    }
}
```

---

## Faza 5: API z Ocelot Gateway i Service Discovery

> **UWAGA:** Ocelot Gateway jest zintegrowany bezpośrednio w projekcie TradingService.Api, co eliminuje potrzebę osobnego projektu Gateway.

### 5.1 Konfiguracja Consul

**Zadania:**

1. **Skrypt inicjalizacji Consul KV (consul-init.ps1)**
   ```powershell
   # consul-init.ps1
   $consulHost = "http://localhost:8500"

   # Trading Service Config
   $tradingConfig = @{
       ScanTime = "04:00"
       Watchlist = @("SLV", "SPY", "QQQ", "AAPL", "MSFT", "GOOGL", "NVDA", "AMD")
       Database = @{
           ConnectionString = "Data Source=trading.db"
       }
       Strategy = @{
           MinExpiryDays = 14
           MaxExpiryDays = 21
           MinConfidence = 0.6
       }
   } | ConvertTo-Json -Depth 3

   Invoke-RestMethod -Uri "$consulHost/v1/kv/TradingService/config" -Method PUT -Body $tradingConfig

   # Broker Config (Exante)
   $brokerConfig = @{
       ApiKey = "YOUR_API_KEY"
       Secret = "YOUR_SECRET"
       AccountId = "YOUR_ACCOUNT"
       Environment = "Demo"
   } | ConvertTo-Json

   Invoke-RestMethod -Uri "$consulHost/v1/kv/TradingService/brokers/Exante" -Method PUT -Body $brokerConfig

   # Loki Config (wypełnić później)
   $lokiConfig = @{
       Endpoint = "http://loki:3100"
       Username = ""
       Password = ""
   } | ConvertTo-Json

   Invoke-RestMethod -Uri "$consulHost/v1/kv/TradingService/logging/loki" -Method PUT -Body $lokiConfig
   ```

2. **ConsulConfigProvider**
   ```csharp
   public class ConsulConfigProvider : IConsulConfigProvider
   {
       private readonly IConsulClient _client;
       private readonly ILogger<ConsulConfigProvider> _logger;
       private TradingServiceConfig? _cachedConfig;
       private DateTime _lastRefresh;
       private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(30);

       public async Task<TradingServiceConfig> GetConfigAsync()
       {
           if (_cachedConfig != null && DateTime.UtcNow - _lastRefresh < _cacheExpiry)
               return _cachedConfig;

           var kvPair = await _client.KV.Get("TradingService/config");
           if (kvPair.Response == null)
               throw new InvalidOperationException("Config not found in Consul");

           var json = Encoding.UTF8.GetString(kvPair.Response.Value);
           _cachedConfig = JsonSerializer.Deserialize<TradingServiceConfig>(json);
           _lastRefresh = DateTime.UtcNow;

           _logger.LogDebug("Configuration refreshed from Consul");

           return _cachedConfig!;
       }

       public async Task<BrokerConfig> GetBrokerConfigAsync(string brokerName)
       {
           var kvPair = await _client.KV.Get($"TradingService/brokers/{brokerName}");
           if (kvPair.Response == null)
               throw new InvalidOperationException($"Broker config for {brokerName} not found");

           var json = Encoding.UTF8.GetString(kvPair.Response.Value);
           return JsonSerializer.Deserialize<BrokerConfig>(json)!;
       }
   }
   ```

### 5.2 TradingService.Api z Ocelot Gateway

**Program.cs (API + Ocelot w jednym projekcie):**
```csharp
using NLog;
using NLog.Web;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;

var logger = LogManager.Setup()
    .LoadConfigurationFromFile("nlog.config")
    .GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Konfiguracja NLog
    builder.Logging.ClearProviders();
    builder.Host.UseNLog();

    // Dodaj konfigurację Ocelot
    builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

    // Serwisy
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Ocelot z Consul
    builder.Services.AddOcelot(builder.Configuration)
        .AddConsul();

    // Consul
    builder.Services.AddSingleton<IConsulClient, ConsulClient>(sp =>
        new ConsulClient(cfg =>
        {
            cfg.Address = new Uri(builder.Configuration["Consul:Host"] ?? "http://localhost:8500");
        }));

    // Rejestracja serwisu w Consul
    builder.Services.AddHostedService<ConsulRegistrationService>();

    // Repozytoria i serwisy
    builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
    builder.Services.AddScoped<IStrategyLoader, StrategyLoader>();

    // CORS
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    var app = builder.Build();

    // Swagger (tylko dev)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();

    // Mapuj kontrolery dla lokalnych endpointów
    app.MapControllers();

    // Ocelot Gateway dla routingu do innych serwisów (jeśli będą potrzebne)
    // await app.UseOcelot();

    logger.Info("TradingService.Api started successfully");
    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Application stopped due to exception");
    throw;
}
finally
{
    LogManager.Shutdown();
}
```

**ocelot.json (konfiguracja routingu - na przyszłość dla mikroserwisów):**
```json
{
  "Routes": [
    {
      "DownstreamPathTemplate": "/api/recommendations/{everything}",
      "DownstreamScheme": "http",
      "DownstreamHostAndPorts": [
        { "Host": "localhost", "Port": 5001 }
      ],
      "UpstreamPathTemplate": "/api/recommendations/{everything}",
      "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE" ]
    }
  ],
  "GlobalConfiguration": {
    "BaseUrl": "http://localhost:5001",
    "ServiceDiscoveryProvider": {
      "Scheme": "http",
      "Host": "localhost",
      "Port": 8500,
      "Type": "Consul"
    }
  }
}
```

### 5.3 Kontrolery API

**RecommendationsController:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class RecommendationsController : ControllerBase
{
    private readonly IRecommendationRepository _repository;
    private readonly ILogger<RecommendationsController> _logger;

    public RecommendationsController(
        IRecommendationRepository repository,
        ILogger<RecommendationsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PutRecommendationDto>>> GetAll()
    {
        var recommendations = await _repository.GetActiveRecommendationsAsync();
        return Ok(recommendations.Select(r => r.ToDto()));
    }

    [HttpGet("short-term")]
    public async Task<ActionResult<IEnumerable<PutRecommendationDto>>> GetShortTerm(
        [FromQuery] int minDays = 14,
        [FromQuery] int maxDays = 21)
    {
        var recommendations = await _repository.GetShortTermRecommendationsAsync(minDays, maxDays);
        return Ok(recommendations.Select(r => r.ToDto()));
    }

    [HttpGet("{symbol}")]
    public async Task<ActionResult<IEnumerable<PutRecommendationDto>>> GetBySymbol(string symbol)
    {
        var recommendations = await _repository.GetBySymbolAsync(symbol);
        return Ok(recommendations.Select(r => r.ToDto()));
    }

    [HttpGet("top/{count:int}")]
    public async Task<ActionResult<IEnumerable<PutRecommendationDto>>> GetTop(int count)
    {
        var recommendations = await _repository.GetActiveRecommendationsAsync();
        return Ok(recommendations.Take(count).Select(r => r.ToDto()));
    }
}
```

**StrategiesController:**
```csharp
[ApiController]
[Route("api/[controller]")]
public class StrategiesController : ControllerBase
{
    private readonly IStrategyLoader _loader;

    [HttpGet]
    public ActionResult<IEnumerable<StrategyInfoDto>> GetAll()
    {
        var strategies = _loader.LoadAllStrategies();
        return Ok(strategies.Select(s => new StrategyInfoDto
        {
            Name = s.Name,
            Description = s.Description,
            TargetExpiryMinDays = s.TargetExpiryMinDays,
            TargetExpiryMaxDays = s.TargetExpiryMaxDays
        }));
    }
}
```

---

## Faza 6: Integracja z Brokerem

### 6.1 Broker Factory Pattern

**Interfejsy:**
```csharp
public interface IBroker
{
    string Name { get; }
    Task<bool> IsConnectedAsync();
    Task<AccountInfo> GetAccountInfoAsync();
    Task<OrderResult> PlacePutSellOrderAsync(PutSellOrder order);
    Task<IEnumerable<Position>> GetPositionsAsync();
    Task<OrderResult> ClosePositionAsync(string positionId);
}

public interface IBrokerFactory
{
    IBroker CreateBroker(string brokerName);
}
```

**BrokerFactory Implementation:**
```csharp
public class BrokerFactory : IBrokerFactory
{
    private readonly IConsulConfigProvider _configProvider;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BrokerFactory> _logger;

    public IBroker CreateBroker(string brokerName)
    {
        _logger.LogDebug("Creating broker: {BrokerName}", brokerName);

        return brokerName.ToLower() switch
        {
            "exante" => CreateExanteBroker(),
            _ => throw new ArgumentException($"Unknown broker: {brokerName}")
        };
    }

    private IBroker CreateExanteBroker()
    {
        var config = _configProvider.GetBrokerConfigAsync("Exante").GetAwaiter().GetResult();
        return ActivatorUtilities.CreateInstance<ExanteBroker>(_serviceProvider, config);
    }
}
```

### 6.2 Exante Broker Implementation

```csharp
public class ExanteBroker : IBroker
{
    private readonly ExanteClient _client;
    private readonly BrokerConfig _config;
    private readonly ILogger<ExanteBroker> _logger;

    public string Name => "Exante";

    public ExanteBroker(BrokerConfig config, ILogger<ExanteBroker> logger)
    {
        _config = config;
        _logger = logger;
        _client = new ExanteClient(new ExanteClientOptions
        {
            ApiKey = config.ApiKey,
            ApiSecret = config.Secret,
            AccountId = config.AccountId,
            Environment = config.Environment == "Live"
                ? ExanteEnvironment.Live
                : ExanteEnvironment.Demo
        });
    }

    public async Task<OrderResult> PlacePutSellOrderAsync(PutSellOrder order)
    {
        try
        {
            _logger.LogInformation(
                "Placing PUT sell order: {Symbol} @ {Strike}, qty {Qty}",
                order.OptionSymbol, order.Strike, order.Quantity);

            var result = await _client.PlaceOrderAsync(new ExanteOrderRequest
            {
                SymbolId = order.OptionSymbol,
                Side = OrderSide.Sell,
                Quantity = order.Quantity,
                OrderType = OrderType.Limit,
                LimitPrice = order.LimitPrice,
                Duration = OrderDuration.Day,
                AccountId = _config.AccountId
            });

            var success = result.Status == "Filled" || result.Status == "Working";

            _logger.LogInformation(
                "Order result: {Status}, OrderId: {OrderId}",
                result.Status, result.OrderId);

            return new OrderResult
            {
                Success = success,
                OrderId = result.OrderId,
                Message = result.StatusMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to place order for {Symbol}", order.OptionSymbol);
            return new OrderResult { Success = false, Message = ex.Message };
        }
    }
}
```

### 6.3 Order Executor

```csharp
public class OrderExecutor : IOrderExecutor
{
    private readonly IBrokerFactory _brokerFactory;
    private readonly ILogger<OrderExecutor> _logger;

    public async Task<OrderResult> ExecuteRecommendationAsync(
        PutRecommendation recommendation,
        decimal investmentAmount,
        string brokerName = "Exante")
    {
        var broker = _brokerFactory.CreateBroker(brokerName);

        var contracts = CalculateContracts(recommendation, investmentAmount);

        var order = new PutSellOrder
        {
            OptionSymbol = BuildOptionSymbol(recommendation),
            Quantity = contracts,
            LimitPrice = recommendation.Premium,
            UnderlyingSymbol = recommendation.Symbol,
            Strike = recommendation.StrikePrice,
            Expiry = recommendation.Expiry
        };

        _logger.LogInformation(
            "Executing order: Sell {Quantity} {Symbol} PUT @ {Strike} (expiry: {Expiry})",
            contracts, recommendation.Symbol, recommendation.StrikePrice,
            recommendation.Expiry.ToShortDateString());

        return await broker.PlacePutSellOrderAsync(order);
    }

    private int CalculateContracts(PutRecommendation rec, decimal investment)
    {
        var marginPerContract = rec.StrikePrice * 100 * 0.2m; // 20% margin
        return (int)(investment / marginPerContract);
    }
}
```

---

## Faza 7: Frontend Vue.js

*(Bez zmian - sekcja pozostaje taka sama jak w oryginalnym planie)*

### 7.1 Inicjalizacja Projektu

```bash
cd frontend
npm create vite@latest . -- --template vue-ts
npm install
npm install -D tailwindcss postcss autoprefixer
npx tailwindcss init -p
npm install axios pinia @vueuse/core
npm install chart.js vue-chartjs
npm install lightweight-charts
```

### 7.2 Konfiguracja Vite

```typescript
// vite.config.ts
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5001',  // Bezpośrednio do API (bez osobnego gateway)
        changeOrigin: true
      }
    }
  }
})
```

---

## Faza 8: Konteneryzacja i Deployment

### 8.1 Dockerfiles

**Dockerfile.TradingService:**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/TradingService/TradingService.csproj", "TradingService/"]
COPY ["src/Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "TradingService/TradingService.csproj"

COPY src/ .
RUN dotnet publish "TradingService/TradingService.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app/publish .

# Utworzenie katalogu na logi
RUN mkdir -p /app/logs

ENV DOTNET_ENVIRONMENT=Production
ENV Consul__Host=consul
ENV Consul__Port=8500

ENTRYPOINT ["dotnet", "TradingService.dll"]
```

**Dockerfile.Api (API + Gateway):**
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/TradingService.Api/TradingService.Api.csproj", "TradingService.Api/"]
COPY ["src/TradingService/TradingService.csproj", "TradingService/"]
COPY ["src/Shared/Shared.csproj", "Shared/"]
RUN dotnet restore "TradingService.Api/TradingService.Api.csproj"

COPY src/ .
RUN dotnet publish "TradingService.Api/TradingService.Api.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .

RUN mkdir -p /app/logs

EXPOSE 5001
ENTRYPOINT ["dotnet", "TradingService.Api.dll"]
```

### 8.2 Docker Compose

```yaml
version: '3.8'

services:
  consul:
    image: consul:latest
    container_name: consul
    ports:
      - "8500:8500"
      - "8600:8600/udp"
    command: agent -server -bootstrap-expect=1 -ui -client=0.0.0.0
    volumes:
      - consul-data:/consul/data
    networks:
      - trading-network

  # Grafana Loki dla logów
  loki:
    image: grafana/loki:latest
    container_name: loki
    ports:
      - "3100:3100"
    command: -config.file=/etc/loki/local-config.yaml
    networks:
      - trading-network

  # Grafana do wizualizacji
  grafana:
    image: grafana/grafana:latest
    container_name: grafana
    ports:
      - "3001:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-data:/var/lib/grafana
    depends_on:
      - loki
    networks:
      - trading-network

  trading-service:
    build:
      context: .
      dockerfile: docker/Dockerfile.TradingService
    container_name: trading-service
    environment:
      - DOTNET_ENVIRONMENT=Production
      - Consul__Host=consul
      - Consul__Port=8500
      - Loki__Endpoint=http://loki:3100
    depends_on:
      - consul
      - loki
    volumes:
      - ./data:/app/data
      - ./logs/trading-service:/app/logs
    networks:
      - trading-network
    restart: unless-stopped

  trading-api:
    build:
      context: .
      dockerfile: docker/Dockerfile.Api
    container_name: trading-api
    ports:
      - "5001:5001"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5001
      - Consul__Host=consul
      - Consul__Port=8500
      - Consul__ServiceName=TradingApi
      - Consul__ServicePort=5001
      - Loki__Endpoint=http://loki:3100
    depends_on:
      - consul
      - trading-service
      - loki
    volumes:
      - ./logs/trading-api:/app/logs
    networks:
      - trading-network
    restart: unless-stopped

  frontend:
    build:
      context: .
      dockerfile: docker/Dockerfile.Frontend
    container_name: trading-frontend
    ports:
      - "3000:80"
    depends_on:
      - trading-api
    networks:
      - trading-network
    restart: unless-stopped

volumes:
  consul-data:
  grafana-data:

networks:
  trading-network:
    driver: bridge
```

---

## Faza 9: Testy i Dokumentacja

### 9.1 Unit Tests

**Struktura testów:**
```
tests/
├── TradingService.Tests/
│   ├── Strategies/
│   │   ├── ShortTermPutStrategyTests.cs
│   │   ├── DividendMomentumStrategyTests.cs
│   │   └── VolatilityCrushStrategyTests.cs
│   ├── Services/
│   │   ├── DailyScanServiceTests.cs
│   │   ├── YahooFinanceProviderTests.cs
│   │   └── OrderExecutorTests.cs
│   └── Repositories/
│       └── RecommendationRepositoryTests.cs
```

**Przykładowy test strategii ShortTermPut:**
```csharp
public class ShortTermPutStrategyTests
{
    private readonly ShortTermPutStrategy _strategy;
    private readonly Mock<ILogger<ShortTermPutStrategy>> _loggerMock;

    public ShortTermPutStrategyTests()
    {
        _loggerMock = new Mock<ILogger<ShortTermPutStrategy>>();
        _strategy = new ShortTermPutStrategy(_loggerMock.Object);
    }

    [Fact]
    public async Task AnalyzeAsync_WithStrongUptrend_ReturnsRecommendation()
    {
        // Arrange
        var marketData = new AggregatedMarketData
        {
            MarketData = new MarketData
            {
                Symbol = "SPY",
                CurrentPrice = 450m,
                RSI = 55m,
                MovingAverage20 = 445m,
                MACD = 2.5m,
                MACDSignal = 1.8m
            },
            TrendAnalysis = new TrendAnalysis
            {
                Symbol = "SPY",
                ExpectedGrowthPercent = 4.5m,
                TrendStrength = 0.7m,
                Direction = TrendDirection.Up
            },
            ShortTermPutOptions = CreateTestOptions("SPY", 450m, 14, 21)
        };

        // Act
        var recommendations = await _strategy.AnalyzeAsync(marketData);

        // Assert
        Assert.NotEmpty(recommendations);
        Assert.All(recommendations, r =>
        {
            Assert.Equal("SPY", r.Symbol);
            Assert.InRange(r.DaysToExpiry, 14, 21);
            Assert.True(r.StrikePrice < 450m);
            Assert.True(r.Confidence >= 0.6m);
        });
    }

    [Fact]
    public async Task AnalyzeAsync_WithWeakTrend_ReturnsEmpty()
    {
        // Arrange
        var marketData = new AggregatedMarketData
        {
            MarketData = new MarketData
            {
                Symbol = "SPY",
                CurrentPrice = 450m,
                RSI = 75m,  // Overbought
                MovingAverage20 = 455m  // Price below MA
            },
            TrendAnalysis = new TrendAnalysis
            {
                ExpectedGrowthPercent = 0.5m,  // Słaby wzrost
                TrendStrength = 0.3m,
                Direction = TrendDirection.Sideways
            },
            ShortTermPutOptions = CreateTestOptions("SPY", 450m, 14, 21)
        };

        // Act
        var recommendations = await _strategy.AnalyzeAsync(marketData);

        // Assert
        Assert.Empty(recommendations);
    }

    private List<OptionContract> CreateTestOptions(string symbol, decimal price, int minDays, int maxDays)
    {
        var options = new List<OptionContract>();
        for (int days = minDays; days <= maxDays; days += 7)
        {
            for (decimal strikeOffset = 0.05m; strikeOffset <= 0.15m; strikeOffset += 0.05m)
            {
                options.Add(new OptionContract
                {
                    Symbol = $"{symbol}_PUT_{days}d",
                    Strike = price * (1 - strikeOffset),
                    Expiry = DateTime.Today.AddDays(days),
                    Bid = 1.5m + strikeOffset * 10,
                    Ask = 1.7m + strikeOffset * 10,
                    ImpliedVolatility = 0.25m,
                    Delta = -0.3m
                });
            }
        }
        return options;
    }
}
```

### 9.2 Dokumentacja

**Zadania:**
1. **README.md** - Przegląd projektu, instalacja, uruchomienie
2. **docs/API.md** - Dokumentacja API endpoints
3. **docs/STRATEGIES.md** - Opis strategii i ich parametrów (z naciskiem na 2-3 tygodniowe opcje)
4. **docs/DEPLOYMENT.md** - Instrukcje deployment (Docker, Windows Service)

---

## Podsumowanie Faz

| Faza | Opis | Zależności |
|------|------|------------|
| 1 | Fundament projektu (NLog, Consul) | - |
| 2 | Warstwa danych | Faza 1 |
| 3 | Integracje zewnętrzne (Yahoo, Exante) | Faza 1, 2 |
| 4 | Silnik strategii (2-3 tyg. opcje PUT) | Faza 2, 3 |
| 5 | API z Ocelot + Consul | Faza 1, 4 |
| 6 | Integracja brokera | Faza 3, 5 |
| 7 | Frontend Vue.js | Faza 5 |
| 8 | Konteneryzacja (+ Grafana Loki) | Faza 1-7 |
| 9 | Testy i dokumentacja | Wszystkie |

---

## Kluczowe Zmiany względem Oryginalnego Planu

1. **Logowanie**: Serilog → **NLog 6.0.7** + **NLog.Targets.Loki 2.2.1** (logi do pliku + Grafana Loki)
2. **Pakiety NuGet**: Zaktualizowane do najnowszych wersji (grudzień 2025)
3. **Strategia główna**: **ShortTermPutStrategy** - opcje PUT z datą zapadalności **14-21 dni** na instrumenty z prognozowanym wzrostem w tym okresie
4. **Architektura**: **ApiGateway połączony z TradingService.Api** - jedna usługa zamiast dwóch
5. **Yahoo Finance**: YahooFinanceApi → **YahooQuotesApi 7.0.5** (aktywniej rozwijany)
6. **Docker Compose**: Dodano **Grafana Loki** i **Grafana** do stacka

---

## Następne Kroki

Po zatwierdzeniu planu, rozpocznij od **Fazy 1** - utworzenia struktury solution i podstawowej konfiguracji projektu z NLog. Każda kolejna faza powinna być realizowana sekwencyjnie, z testami i walidacją po zakończeniu każdego etapu.
