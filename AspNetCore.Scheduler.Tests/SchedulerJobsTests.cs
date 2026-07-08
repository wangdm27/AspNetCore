using FluentAssertions;
using Microsoft.Extensions.Configuration;

using AspNetCore.Scheduler.Jobs;

using Microsoft.Extensions.Logging.Abstractions;

namespace AspNetCore.Scheduler.Tests;

/// <summary>
/// HeartbeatJob / LogCleanupJob 单元测试：纯任务执行（记录日志，返回已完成）。
/// </summary>
public class SchedulerJobsTests
{
    [Fact]
    public async Task HeartbeatJob_RunAsync_CompletesSuccessfully()
    {
        // Arrange
        var job = new HeartbeatJob(NullLogger<HeartbeatJob>.Instance);

        // Act
        await job.RunAsync();

        // Assert - 无异常即成功（任务仅记日志）
        Assert.True(true);
    }

    [Fact]
    public async Task HeartbeatJob_RunAsync_WithCancellation_CompletesOrCancels()
    {
        // Arrange - 任务不检查 token，应正常完成
        var job = new HeartbeatJob(NullLogger<HeartbeatJob>.Instance);
        using var cts = new CancellationTokenSource();

        // Act
        await job.RunAsync(cts.Token);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task HeartbeatJob_IsSchedulerJob()
    {
        // Arrange - 实现 ISchedulerJob
        var job = new HeartbeatJob(NullLogger<HeartbeatJob>.Instance);

        // Assert
        job.Should().BeAssignableTo<ISchedulerJob>();
        await job.RunAsync(); // 可调用
    }

    [Fact]
    public async Task LogCleanupJob_RunAsync_CompletesSuccessfully()
    {
        // Arrange
        var job = new LogCleanupJob(NullLogger<LogCleanupJob>.Instance);

        // Act
        await job.RunAsync();

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task LogCleanupJob_IsSchedulerJob()
    {
        // Arrange
        var job = new LogCleanupJob(NullLogger<LogCleanupJob>.Instance);

        // Assert
        job.Should().BeAssignableTo<ISchedulerJob>();
        await job.RunAsync();
    }

    [Fact]
    public async Task HeartbeatJob_RunAsync_ReturnsCompletedTask()
    {
        // Arrange
        var job = new HeartbeatJob(NullLogger<HeartbeatJob>.Instance);

        // Act
        var task = job.RunAsync();

        // Assert - 返回已完成任务（同步完成）
        task.IsCompleted.Should().BeTrue();
        await task;
    }
}
