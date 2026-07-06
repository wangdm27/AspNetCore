namespace AspNetCore.Scheduler.Jobs;

/// <summary>
/// 示例: 每分钟心跳日志。
/// </summary>
public class HeartbeatJob(ILogger<HeartbeatJob> log) : ISchedulerJob
{
    public Task RunAsync(CancellationToken ct = default)
    {
        log.LogInformation("heartbeat tick at {Time}", DateTimeOffset.Now);
        return Task.CompletedTask;
    }
}
