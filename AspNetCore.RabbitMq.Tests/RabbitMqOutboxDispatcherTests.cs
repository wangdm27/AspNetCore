using System.Reflection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;

using AspNetCore.RabbitMq;

using Moq;
using RabbitMQ.Client;

namespace AspNetCore.RabbitMq.Tests;

/// <summary>
/// RabbitMqOutboxDispatcher 单元测试。
/// internal sealed BackgroundService，经 InternalsVisibleTo 可见。
/// ExecuteAsync 为 protected，通过反射调用并用 CancellationToken 控制循环退出。
/// mock IRabbitMqOutboxStore + IRabbitMqPublisher，纯逻辑不连 RabbitMQ。
/// </summary>
public class RabbitMqOutboxDispatcherTests
{
    /// <summary>反射调用 protected ExecuteAsync，返回其 Task。</summary>
    private static Task InvokeExecuteAsync(RabbitMqOutboxDispatcher dispatcher, CancellationToken token)
    {
        var method = typeof(RabbitMqOutboxDispatcher).GetMethod(
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull("ExecuteAsync 应存在于 RabbitMqOutboxDispatcher");
        return (Task)method!.Invoke(dispatcher, new object[] { token })!;
    }

    /// <summary>构造测试用 Options：调度间隔极小，便于快速循环；重试上限 5。</summary>
    private static RabbitMqOptions CreateOptions() => new()
    {
        OutboxDispatchInterval = TimeSpan.FromMilliseconds(1),
        OutboxBatchSize = 10,
        MaxRetryCount = 5,
        RetryBaseDelay = TimeSpan.FromSeconds(1),
        RetryMaxDelay = TimeSpan.FromSeconds(30),
        DeadLetterExchange = "dlx",
        DeadLetterRoutingKey = "dlx.rk",
    };

    /// <summary>构造一条待发消息。</summary>
    private static RabbitMqOutboxMessage NewMessage(int retryCount = 0) => new()
    {
        Exchange = "orders",
        RoutingKey = "created",
        Body = new byte[] { 1, 2, 3 },
        Headers = new Dictionary<string, object?> { ["x"] = "y" },
        RetryCount = retryCount,
    };

    private static Mock<IRabbitMqPublisher> SetupPublisher()
    {
        var publisherMock = new Mock<IRabbitMqPublisher>();
        publisherMock.Setup(p => p.PublishRawAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<Action<IBasicProperties>?>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        return publisherMock;
    }

    [Fact]
    public async Task ExecuteAsync_PublishSucceeds_MarksAsPublished()
    {
        // Arrange - 取到待发消息 -> 发布成功 -> MarkAsPublished
        var storeMock = new Mock<IRabbitMqOutboxStore>();
        var publisherMock = SetupPublisher();
        var options = CreateOptions();
        var logger = NullLogger<RabbitMqOutboxDispatcher>.Instance;
        var msg = NewMessage();

        // 安全超时：回调未取消时 2s 后兜底取消，防止挂死
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        storeMock.Setup(s => s.GetPendingAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RabbitMqOutboxMessage> { msg });

        // 发布成功后取消，使循环在 Task.Delay 处退出
        storeMock.Setup(s => s.MarkAsPublishedAsync(
                It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        var dispatcher = new RabbitMqOutboxDispatcher(
            storeMock.Object, publisherMock.Object, options, logger);

        // Act
        await InvokeExecuteAsync(dispatcher, cts.Token);

        // Assert - 发布到原交换机一次，标记已发布，未走失败/死信
        publisherMock.Verify(p => p.PublishRawAsync(
            msg.Exchange, msg.RoutingKey, It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IDictionary<string, object?>?>(), It.IsAny<Action<IBasicProperties>?>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);

        storeMock.Verify(s => s.MarkAsPublishedAsync(
            msg.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        storeMock.Verify(s => s.MarkAsFailedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        storeMock.Verify(s => s.MarkAsDeadLetterAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_PublishFailsWithRetriesLeft_MarksAsFailed()
    {
        // Arrange - RetryCount=0, MaxRetryCount=5 -> newRetry=1 < 5 -> MarkAsFailed（指数退避）
        var storeMock = new Mock<IRabbitMqOutboxStore>();
        var publisherMock = SetupPublisher();
        var options = CreateOptions();
        var logger = NullLogger<RabbitMqOutboxDispatcher>.Instance;
        var msg = NewMessage(retryCount: 0);
        const string errorMsg = "broker unavailable";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // 原交换机发布抛异常（区分死信交换机：原 Exchange=orders）
        publisherMock.Setup(p => p.PublishRawAsync(
                It.Is<string>(e => e == msg.Exchange),
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<Action<IBasicProperties>?>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => throw new InvalidOperationException(errorMsg))
            .Returns(ValueTask.CompletedTask);

        storeMock.Setup(s => s.GetPendingAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RabbitMqOutboxMessage> { msg });

        DateTimeOffset? capturedNextAttempt = null;
        storeMock.Setup(s => s.MarkAsFailedAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .Callback<Guid, string, DateTimeOffset, CancellationToken>((_, _, next, _) =>
            {
                capturedNextAttempt = next;
                cts.Cancel();
            })
            .Returns(Task.CompletedTask);

        var dispatcher = new RabbitMqOutboxDispatcher(
            storeMock.Object, publisherMock.Object, options, logger);

        // Act
        await InvokeExecuteAsync(dispatcher, cts.Token);

        // Assert - 标记失败并设置未来重试时间，未走已发布/死信
        storeMock.Verify(s => s.MarkAsFailedAsync(
            msg.Id, errorMsg, It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
        storeMock.Verify(s => s.MarkAsPublishedAsync(
            It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        storeMock.Verify(s => s.MarkAsDeadLetterAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        // 退避：base=1s * 2^1 = 2s -> nextAttempt 在未来
        capturedNextAttempt.Should().NotBeNull();
        capturedNextAttempt!.Value.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task ExecuteAsync_PublishFailsRetryExhausted_MarksAsDeadLetterAndPublishesToDlx()
    {
        // Arrange - RetryCount=4, MaxRetryCount=5 -> 发布失败 newRetry=5 >= 5 -> 死信
        var storeMock = new Mock<IRabbitMqOutboxStore>();
        var publisherMock = SetupPublisher();
        var options = CreateOptions();
        var logger = NullLogger<RabbitMqOutboxDispatcher>.Instance;
        var msg = NewMessage(retryCount: 4);
        const string errorMsg = "broker down";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // 原交换机发布抛异常；死信交换机发布成功（SetupPublisher 默认成功）
        publisherMock.Setup(p => p.PublishRawAsync(
                It.Is<string>(e => e == msg.Exchange),
                It.IsAny<string>(),
                It.IsAny<ReadOnlyMemory<byte>>(),
                It.IsAny<IDictionary<string, object?>?>(),
                It.IsAny<Action<IBasicProperties>?>(),
                It.IsAny<bool>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback(() => throw new InvalidOperationException(errorMsg))
            .Returns(ValueTask.CompletedTask);

        storeMock.Setup(s => s.GetPendingAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RabbitMqOutboxMessage> { msg });

        // MarkAsDeadLetter 后取消（死信交换机发布仍会被调用，mock 不受 token 影响）
        storeMock.Setup(s => s.MarkAsDeadLetterAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        var dispatcher = new RabbitMqOutboxDispatcher(
            storeMock.Object, publisherMock.Object, options, logger);

        // Act
        await InvokeExecuteAsync(dispatcher, cts.Token);

        // Assert - 标记死信；原交换机发布一次（失败）；死信交换机发布一次
        storeMock.Verify(s => s.MarkAsDeadLetterAsync(
            msg.Id, It.IsAny<CancellationToken>()), Times.Once);
        storeMock.Verify(s => s.MarkAsFailedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        storeMock.Verify(s => s.MarkAsPublishedAsync(
            It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);

        publisherMock.Verify(p => p.PublishRawAsync(
            msg.Exchange, msg.RoutingKey, It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IDictionary<string, object?>?>(), It.IsAny<Action<IBasicProperties>?>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        publisherMock.Verify(p => p.PublishRawAsync(
            options.DeadLetterExchange, options.DeadLetterRoutingKey, It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IDictionary<string, object?>?>(), It.IsAny<Action<IBasicProperties>?>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RetryCountAlreadyExhausted_DeadLettersWithoutPublishAttempt()
    {
        // Arrange - RetryCount=5 >= MaxRetryCount=5 -> 进入 foreach 顶部预检，直接死信，不尝试原发布
        var storeMock = new Mock<IRabbitMqOutboxStore>();
        var publisherMock = SetupPublisher();
        var options = CreateOptions();
        var logger = NullLogger<RabbitMqOutboxDispatcher>.Instance;
        var msg = NewMessage(retryCount: 5);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        storeMock.Setup(s => s.GetPendingAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<RabbitMqOutboxMessage> { msg });

        storeMock.Setup(s => s.MarkAsDeadLetterAsync(
                It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .Returns(Task.CompletedTask);

        var dispatcher = new RabbitMqOutboxDispatcher(
            storeMock.Object, publisherMock.Object, options, logger);

        // Act
        await InvokeExecuteAsync(dispatcher, cts.Token);

        // Assert - 未尝试发布到原交换机；仅死信交换机发布一次；标记死信
        publisherMock.Verify(p => p.PublishRawAsync(
            msg.Exchange, msg.RoutingKey, It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IDictionary<string, object?>?>(), It.IsAny<Action<IBasicProperties>?>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.PublishRawAsync(
            options.DeadLetterExchange, options.DeadLetterRoutingKey, It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IDictionary<string, object?>?>(), It.IsAny<Action<IBasicProperties>?>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        storeMock.Verify(s => s.MarkAsDeadLetterAsync(
            msg.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyQueue_WaitsAndLoopsWithoutCrashing()
    {
        // Arrange - 空队列：foreach 不执行，循环等待不崩；第 2 次拉取时取消退出
        var storeMock = new Mock<IRabbitMqOutboxStore>();
        var publisherMock = SetupPublisher();
        var options = CreateOptions();
        var logger = NullLogger<RabbitMqOutboxDispatcher>.Instance;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var fetchCount = 0;
        storeMock.Setup(s => s.GetPendingAsync(
                It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback(() => { fetchCount++; if (fetchCount >= 2) cts.Cancel(); })
            .ReturnsAsync(new List<RabbitMqOutboxMessage>());

        var dispatcher = new RabbitMqOutboxDispatcher(
            storeMock.Object, publisherMock.Object, options, logger);

        // Act - 至少循环两轮（证明等待后能继续），不抛异常
        var act = () => InvokeExecuteAsync(dispatcher, cts.Token);

        // Assert
        await act.Should().CompleteWithinAsync(TimeSpan.FromSeconds(3));
        fetchCount.Should().BeGreaterThanOrEqualTo(2);
        storeMock.Verify(s => s.MarkAsPublishedAsync(
            It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        storeMock.Verify(s => s.MarkAsFailedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Never);
        storeMock.Verify(s => s.MarkAsDeadLetterAsync(
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.PublishRawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IDictionary<string, object?>?>(), It.IsAny<Action<IBasicProperties>?>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyCancelled_ExitsImmediatelyWithoutFetching()
    {
        // Arrange - 启动前已取消 -> while 条件不满足，直接退出，不拉取消息
        var storeMock = new Mock<IRabbitMqOutboxStore>();
        var publisherMock = SetupPublisher();
        var options = CreateOptions();
        var logger = NullLogger<RabbitMqOutboxDispatcher>.Instance;

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var dispatcher = new RabbitMqOutboxDispatcher(
            storeMock.Object, publisherMock.Object, options, logger);

        // Act
        await InvokeExecuteAsync(dispatcher, cts.Token);

        // Assert - 优雅退出，无任何存储/发布调用
        storeMock.Verify(s => s.GetPendingAsync(
            It.IsAny<DateTimeOffset>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        publisherMock.Verify(p => p.PublishRawAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ReadOnlyMemory<byte>>(),
            It.IsAny<IDictionary<string, object?>?>(), It.IsAny<Action<IBasicProperties>?>(),
            It.IsAny<bool>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
