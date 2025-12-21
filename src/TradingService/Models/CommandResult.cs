namespace TradingService.Models;

public class CommandResult<T>
{
    public T? Data { get; set; }
    public bool Success => !Errors.Any() && ValidationSuccess;
    public bool ValidationSuccess => !ValidationErrors.Any();
    public List<string> Errors { get; set; } = [];
    public List<string> ValidationErrors { get; set; } = [];
}

public class Result<T>
{
    public T? Data { get; set; }
    public bool IsValid => !Errors.Any();
    public List<string> Errors { get; set; } = [];
}

public class GridResult<T> : Result<T>
{
    public int TotalCount { get; set; }
}
