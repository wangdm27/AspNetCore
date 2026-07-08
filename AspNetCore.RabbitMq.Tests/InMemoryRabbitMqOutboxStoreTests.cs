using FluentAssertions;

using AspNetCore.RabbitMq;

namespace AspNetCore.RabbitMq.Tests;

/// <summary>
/// InMemoryRabbitMqOutboxStore 单元测试：Add/GetPending 过滤排序限数/MarkAs*。
/// internal 类经 InternalsVisibleTo 暴露。纯内存逻辑，无外部依赖。
/// </summary>
public class InMemoryRabbitMqOutboxStoreTests
{
    private static RabbitMqOutboxMessage NewMessage(
        DateTimeOffset createdAt,
        DateTimeOffset? nextAttemptAt = null)
        => new()
        {
            Exchange = "ex",
            RoutingKey = "rk",
            Body = new byte[] { 1 },
            CreatedAt = createdAt,
            NextAttemptAt = nextAttemptAt
        };

    private static InMemoryRabbitMqOutboxStore CreateStore() => new();

    [Fact]
    public async Task AddAsync_NewMessage_StoresByMessageId()
    {
        // Arrange
        var store = CreateStore();
        var msg = NewMessage(DateTimeOffset.UtcNow);

        // Act
        await store.AddAsync(msg);

        // Assert — GetPending 能取到
        var pending = await store.GetPendingAsync(DateTimeOffset.UtcNow, 10);
        pending.Should().ContainSingle().Which.Id.Should().Be(msg.Id);
    }

    [Fact]
    public async Task GetPendingAsync_ExcludesPublishedMessages()
    {
        // Arrange
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var msg = NewMessage(now);
        await store.AddAsync(msg);
        await store.MarkAsPublishedAsync(msg.Id, now);

        // Act
        var pending = await store.GetPendingAsync(now, 10);

        // Assert
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingAsync_ExcludesDeadLetteredMessages()
    {
        // Arrange
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var msg = NewMessage(now);
        await store.AddAsync(msg);
        await store.MarkAsDeadLetterAsync(msg.Id);

        // Act
        var pending = await store.GetPendingAsync(now, 10);

        // Assert
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingAsync_ExcludesMessagesNotYetDueForRetry()
    {
        // Arrange — NextAttemptAt 在未来
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var future = now.AddMinutes(5);
        var msg = NewMessage(now, nextAttemptAt: future);
        await store.AddAsync(msg);

        // Act
        var pending = await store.GetPendingAsync(now, 10);

        // Assert
        pending.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPendingAsync_IncludesMessagesDueForRetry()
    {
        // Arrange — NextAttemptAt 已到
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var due = now.AddMinutes(-1);
        var msg = NewMessage(now, nextAttemptAt: due);
        await store.AddAsync(msg);

        // Act
        var pending = await store.GetPendingAsync(now, 10);

        // Assert
        pending.Should().ContainSingle().Which.Id.Should().Be(msg.Id);
    }

    [Fact]
    public async Task GetPendingAsync_IncludesMessagesWithNullNextAttempt()
    {
        // Arrange — null 表示立即可重试
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var msg = NewMessage(now, nextAttemptAt: null);
        await store.AddAsync(msg);

        // Act
        var pending = await store.GetPendingAsync(now, 10);

        // Assert
        pending.Should().ContainSingle();
    }

    [Fact]
    public async Task GetPendingAsync_OrdersByCreatedAtAscending()
    {
        // Arrange
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var older = NewMessage(now.AddSeconds(-10));
        var newer = NewMessage(now.AddSeconds(-2));
        var oldest = NewMessage(now.AddSeconds(-30));
        await store.AddAsync(newer);
        await store.AddAsync(oldest);
        await store.AddAsync(older);

        // Act
        var pending = await store.GetPendingAsync(now, 10);

        // Assert — 按创建时间升序
        pending.Should().HaveCount(3);
        pending[0].Id.Should().Be(oldest.Id);
        pending[1].Id.Should().Be(older.Id);
        pending[2].Id.Should().Be(newer.Id);
    }

    [Fact]
    public async Task GetPendingAsync_LimitsByTakeCount()
    {
        // Arrange
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            await store.AddAsync(NewMessage(now.AddSeconds(-i)));
        }

        // Act
        var pending = await store.GetPendingAsync(now, 2);

        // Assert
        pending.Should().HaveCount(2);
    }

    [Fact]
    public async Task MarkAsFailedAsync_RecordsErrorAndIncrementsRetryAndSetsNextAttempt()
    {
        // Arrange
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var msg = NewMessage(now);
        await store.AddAsync(msg);
        var nextAttempt = now.AddSeconds(5);
        const string error = "broker unavailable";

        // Act
        await store.MarkAsFailedAsync(msg.Id, error, nextAttempt);

        // Assert — 仍可被取到（NextAttemptAt 已到）
        var pending = await store.GetPendingAsync(nextAttempt, 10);
        var updated = pending.Should().ContainSingle().Subject;
        updated.RetryCount.Should().Be(1);
        updated.LastError.Should().Be(error);
        updated.NextAttemptAt.Should().Be(nextAttempt);
    }

    [Fact]
    public async Task MarkAsFailedAsync_Twice_IncrementsRetryCountTwice()
    {
        // Arrange
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var msg = NewMessage(now);
        await store.AddAsync(msg);

        // Act
        await store.MarkAsFailedAsync(msg.Id, "err1", now.AddSeconds(1));
        await store.MarkAsFailedAsync(msg.Id, "err2", now.AddSeconds(2));

        // Assert
        var pending = await store.GetPendingAsync(now.AddSeconds(2), 10);
        var updated = pending.Should().ContainSingle().Subject;
        updated.RetryCount.Should().Be(2);
        updated.LastError.Should().Be("err2");
    }

    [Fact]
    public async Task MarkAsPublishedAsync_OnUnknownId_DoesNotThrow()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var act = () => store.MarkAsPublishedAsync(Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Assert — 静默处理不存在消息
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MarkAsFailedAsync_OnUnknownId_DoesNotThrow()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var act = () => store.MarkAsFailedAsync(Guid.NewGuid(), "x", DateTimeOffset.UtcNow);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task MarkAsDeadLetterAsync_OnUnknownId_DoesNotThrow()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var act = () => store.MarkAsDeadLetterAsync(Guid.NewGuid());

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AddAsync_OverwriteSameId_KeepsLatest()
    {
        // Arrange — Add 用 message.Id 作键，同 Id 覆盖
        var store = CreateStore();
        var now = DateTimeOffset.UtcNow;
        var msg = NewMessage(now);
        await store.AddAsync(msg);
        var duplicate = new RabbitMqOutboxMessage
        {
            Id = msg.Id,
            Exchange = "ex2",
            RoutingKey = "rk2",
            Body = new byte[] { 2 },
            CreatedAt = now
        };

        // Act
        await store.AddAsync(duplicate);

        // Assert
        var pending = await store.GetPendingAsync(now, 10);
        pending.Should().ContainSingle().Which.RoutingKey.Should().Be("rk2");
    }
}
