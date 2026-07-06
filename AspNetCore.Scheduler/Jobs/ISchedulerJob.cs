namespace AspNetCore.Scheduler.Jobs;

/// <summary>
/// 定时任务抽象。
/// </summary>
public interface ISchedulerJob
{
    Task RunAsync(CancellationToken ct = default);
}
