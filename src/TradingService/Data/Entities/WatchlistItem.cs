using LinqToDB.Mapping;

namespace TradingService.Data.Entities;

[Table("Watchlist")]
public class WatchlistItem
{
    [PrimaryKey, Identity]
    public int Id { get; set; }

    [Column, NotNull]
    public string Symbol { get; set; } = string.Empty;

    [Column]
    public bool IsActive { get; set; } = true;

    [Column]
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    [Column]
    public string? Notes { get; set; }
}
