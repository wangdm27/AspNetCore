using FluentAssertions;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using AspNetCore.RabbitMq;

namespace AspNetCore.RabbitMq.Tests;

/// <summary>
/// ChannelConfirmTracker 单元测试：发布确认追踪。
/// internal 类经 InternalsVisibleTo 暴露。mock IChannel，用 mock.Raise 触发 ack/nack 事件回调。
/// </summary>
public class ChannelConfirmTrackerTests
{
    private static Mock<IChannel> CreateChannelMock() => new();

    [Fact]
    public async Task Register_NewSequence_ReturnsUncompletedTcs()
    {
        // Arrange
        var channelMock = CreateChannelMock();
        await using var tracker = new ChannelConfirmTracker(channelMock.Object);

        // Act
        var tcs = tracker.Register(1UL);

        // Assert
        tcs.Task.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task WaitAsync_UnknownSequence_ReturnsConfirmedImmediately()
    {
        // Arrange — 未注册的序列号视为已确认
        var channelMock = CreateChannelMock();
        await using var tracker = new ChannelConfirmTracker(channelMock.Object);

        // Act
        var result = await tracker.WaitAsync(999UL, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        // Assert
        result.Should().Be(PublishConfirmResult.Confirmed);
    }

    [Fact]
    public async Task WaitAsync_TimeoutWithoutAck_ReturnsTimedOut()
    {
        // Arrange
        var channelMock = CreateChannelMock();
        await using var tracker = new ChannelConfirmTracker(channelMock.Object);
        tracker.Register(1UL);

        // Act
        var result = await tracker.WaitAsync(1UL, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        // Assert
        result.Should().Be(PublishConfirmResult.TimedOut);
    }

    [Fact]
    public async Task WaitAsync_AfterAck_ReturnsConfirmed()
    {
        // Arrange
        var channelMock = CreateChannelMock();
        await using var tracker = new ChannelConfirmTracker(channelMock.Object);
        tracker.Register(1UL);

        // Act — 单条 ack
        channelMock.Raise(
            c => c.BasicAcksAsync += null,
            channelMock.Object,
            new BasicAckEventArgs(1UL, multiple: false, CancellationToken.None));
        var result = await tracker.WaitAsync(1UL, TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        result.Should().Be(PublishConfirmResult.Confirmed);
    }

    [Fact]
    public async Task WaitAsync_AfterNack_ReturnsNacked()
    {
        // Arrange
        var channelMock = CreateChannelMock();
        await using var tracker = new ChannelConfirmTracker(channelMock.Object);
        tracker.Register(2UL);

        // Act
        channelMock.Raise(
            c => c.BasicNacksAsync += null,
            channelMock.Object,
            new BasicNackEventArgs(2UL, multiple: false, requeue: false, CancellationToken.None));
        var result = await tracker.WaitAsync(2UL, TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        result.Should().Be(PublishConfirmResult.Nacked);
    }

    [Fact]
    public async Task WaitAsync_AfterAckMultiple_CompletesAllUpToDeliveryTag()
    {
        // Arrange
        var channelMock = CreateChannelMock();
        await using var tracker = new ChannelConfirmTracker(channelMock.Object);
        tracker.Register(1UL);
        tracker.Register(2UL);
        tracker.Register(3UL);

        // Act — Multiple=true，deliveryTag=2 完成 ≤2 的全部
        channelMock.Raise(
            c => c.BasicAcksAsync += null,
            channelMock.Object,
            new BasicAckEventArgs(2UL, multiple: true, CancellationToken.None));
        var r1 = await tracker.WaitAsync(1UL, TimeSpan.FromSeconds(1), CancellationToken.None);
        var r2 = await tracker.WaitAsync(2UL, TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        r1.Should().Be(PublishConfirmResult.Confirmed);
        r2.Should().Be(PublishConfirmResult.Confirmed);
    }

    [Fact]
    public async Task WaitAsync_AfterNackMultiple_CompletesAllUpToDeliveryTagAsNacked()
    {
        // Arrange
        var channelMock = CreateChannelMock();
        await using var tracker = new ChannelConfirmTracker(channelMock.Object);
        tracker.Register(1UL);
        tracker.Register(2UL);

        // Act
        channelMock.Raise(
            c => c.BasicNacksAsync += null,
            channelMock.Object,
            new BasicNackEventArgs(2UL, multiple: true, requeue: false, CancellationToken.None));
        var r1 = await tracker.WaitAsync(1UL, TimeSpan.FromSeconds(1), CancellationToken.None);
        var r2 = await tracker.WaitAsync(2UL, TimeSpan.FromSeconds(1), CancellationToken.None);

        // Assert
        r1.Should().Be(PublishConfirmResult.Nacked);
        r2.Should().Be(PublishConfirmResult.Nacked);
    }

    [Fact]
    public async Task Remove_BeforeAck_MakesWaitReturnConfirmed()
    {
        // Arrange
        var channelMock = CreateChannelMock();
        await using var tracker = new ChannelConfirmTracker(channelMock.Object);
        tracker.Register(5UL);

        // Act
        tracker.Remove(5UL);

        // Assert — 移除后 WaitAsync 走"未注册→Confirmed"分支
        var result = await tracker.WaitAsync(5UL, TimeSpan.FromMilliseconds(50), CancellationToken.None);

        result.Should().Be(PublishConfirmResult.Confirmed);
    }

    [Fact]
    public async Task DisposeAsync_CompletesPendingWithObjectDisposedException()
    {
        // Arrange
        var channelMock = CreateChannelMock();
        var tracker = new ChannelConfirmTracker(channelMock.Object);
        var tcs = tracker.Register(7UL);

        // Act
        await tracker.DisposeAsync();

        // Assert
        tcs.Task.IsFaulted.Should().BeTrue();
        tcs.Task.Exception!.InnerExceptions
            .Should().ContainSingle().Which
            .Should().BeOfType<ObjectDisposedException>();
    }

    [Fact]
    public async Task WaitAsync_AfterCancellation_PropagatesCancellation()
    {
        // Arrange — 已注册但未确认，取消令牌触发
        var channelMock = CreateChannelMock();
        await using var tracker = new ChannelConfirmTracker(channelMock.Object);
        tracker.Register(11UL);
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(30));

        // Act
        var act = () => tracker.WaitAsync(11UL, TimeSpan.FromSeconds(5), cts.Token);

        // Assert — 取消向上抛（区别于超时返回 TimedOut）
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
