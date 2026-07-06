namespace AspNetCore.Scheduler.Jobs;

/// <summary>
/// 示例: 每日 02:00 清理占位。
/// </summary>
public class LogCleanupJob(ILogger<LogCleanupJob> log) : ISchedulerJob
{
    public Task RunAsync(CancellationToken ct = default)
    {
        log.LogInformation("log cleanup placeholder run");
        return Task.CompletedTask;
    }
}
