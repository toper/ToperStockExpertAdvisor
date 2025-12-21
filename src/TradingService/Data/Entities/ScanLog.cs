using LinqToDB.Mapping;

namespace TradingService.Data.Entities;

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
