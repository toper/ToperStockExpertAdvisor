using LinqToDB;
using Microsoft.Extensions.Logging;
using TradingService.Data;
using TradingService.Data.Entities;
using TradingService.Services.Interfaces;

namespace TradingService.Services.Repositories;

/// <summary>
/// Repository for managing company financial data from SimFin
/// </summary>
public class CompanyFinancialRepository : ICompanyFinancialRepository
{
    private readonly IDbContextFactory _dbContextFactory;
    private readonly ILogger<CompanyFinancialRepository> _logger;

    public CompanyFinancialRepository(
        IDbContextFactory dbContextFactory,
        ILogger<CompanyFinancialRepository> logger)
    {
        _dbContextFactory = dbContextFactory;
        _logger = logger;
    }

    public async Task<CompanyFinancial?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            // Get most recent financial data for the symbol
            var financial = await db.CompanyFinancials
                .Where(cf => cf.Symbol == symbol)
                .OrderByDescending(cf => cf.ReportDate)
                .FirstOrDefaultAsync(cancellationToken);

            return financial;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting financial data for symbol {Symbol}", symbol);
            throw;
        }
    }

    public async Task<List<CompanyFinancial>> GetByFScoreThresholdAsync(decimal minFScore, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            // Get symbols with F-Score > threshold (most recent report per symbol)
            var financials = await db.CompanyFinancials
                .Where(cf => cf.PiotroskiFScore != null && cf.PiotroskiFScore > minFScore)
                .GroupBy(cf => cf.Symbol)
                .Select(g => g.OrderByDescending(cf => cf.ReportDate).First())
                .OrderByDescending(cf => cf.PiotroskiFScore)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Found {Count} companies with F-Score > {MinFScore}",
                financials.Count, minFScore);

            return financials;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting companies by F-Score threshold {MinFScore}", minFScore);
            throw;
        }
    }

    public async Task BulkInsertOrUpdateAsync(IEnumerable<CompanyFinancial> financials, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var financialsList = financials.ToList();
            if (!financialsList.Any())
            {
                _logger.LogWarning("No financial data to insert");
                return;
            }

            // Use InsertOrReplace for UPSERT logic
            // This will insert new records or replace existing ones based on unique index (Symbol, ReportDate)
            var inserted = 0;
            foreach (var financial in financialsList)
            {
                await db.InsertOrReplaceAsync(financial, token: cancellationToken);
                inserted++;

                if (inserted % 100 == 0)
                {
                    _logger.LogDebug("Inserted/updated {Count}/{Total} company financials",
                        inserted, financialsList.Count);
                }
            }

            _logger.LogInformation("Bulk inserted/updated {Count} company financial records", inserted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk inserting company financials");
            throw;
        }
    }

    public async Task<int> GetCountByFScoreAsync(decimal minFScore, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            // Count distinct symbols with F-Score >= threshold
            var count = await db.CompanyFinancials
                .Where(cf => cf.PiotroskiFScore != null && cf.PiotroskiFScore >= minFScore)
                .Select(cf => cf.Symbol)
                .Distinct()
                .CountAsync(cancellationToken);

            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting companies by F-Score");
            throw;
        }
    }

    public async Task<bool> IsDataStaleAsync(TimeSpan maxAge, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            // Check if any financial data exists
            var anyDataExists = await db.CompanyFinancials.AnyAsync(cancellationToken);
            if (!anyDataExists)
            {
                _logger.LogInformation("No financial data exists in database - data is stale");
                return true;
            }

            // Get the most recent FetchedAt date
            var mostRecentFetchDate = await db.CompanyFinancials
                .OrderByDescending(cf => cf.FetchedAt)
                .Select(cf => cf.FetchedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (mostRecentFetchDate == default)
            {
                return true;
            }

            var cutoffDate = DateTime.UtcNow - maxAge;
            var isStale = mostRecentFetchDate < cutoffDate;

            _logger.LogInformation(
                "Financial data staleness check: Most recent fetch {MostRecent}, Cutoff {Cutoff}, Is stale: {IsStale}",
                mostRecentFetchDate, cutoffDate, isStale);

            return isStale;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking data staleness");
            throw;
        }
    }

    public async Task<List<CompanyFinancial>> GetAllOrderedByFScoreAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var financials = await db.CompanyFinancials
                .Where(cf => cf.PiotroskiFScore != null)
                .GroupBy(cf => cf.Symbol)
                .Select(g => g.OrderByDescending(cf => cf.ReportDate).First())
                .OrderByDescending(cf => cf.PiotroskiFScore)
                .Take(limit)
                .ToListAsync(cancellationToken);

            return financials;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting top companies by F-Score");
            throw;
        }
    }

    public async Task DeleteOlderThanAsync(DateTime cutoffDate, CancellationToken cancellationToken = default)
    {
        try
        {
            using var db = _dbContextFactory.Create();

            var deletedCount = await db.CompanyFinancials
                .Where(cf => cf.FetchedAt < cutoffDate)
                .DeleteAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} stale financial records older than {CutoffDate}",
                deletedCount, cutoffDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting stale financial data");
            throw;
        }
    }
}
