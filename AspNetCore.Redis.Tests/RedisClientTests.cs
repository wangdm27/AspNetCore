using FluentAssertions;
using Moq;
using StackExchange.Redis;

using AspNetCore.Redis;

namespace AspNetCore.Redis.Tests;

/// <summary>
/// RedisClient 单元测试：mock IConnectionMultiplexer + IDatabase，验证 BuildKey 前缀拼接与方法转发。
/// 注：AspNetCore.Redis.RedisKey（业务键生成器）与 StackExchange.Redis.RedisKey（驱动键类型）同名，
/// 测试中用完全限定名 StackExchange.Redis.RedisKey/RedisValue 消歧。
/// Expiration.Default 引用来源不明（见报告），测试一律显式传 expiry 避开 ?? 分支。
/// </summary>
public class RedisClientTests
{
    private const string Prefix = "app:";

    private static (Mock<IConnectionMultiplexer> mux, Mock<IDatabase> db, RedisClient client) Create()
    {
        var dbMock = new Mock<IDatabase>();
        var muxMock = new Mock<IConnectionMultiplexer>();
        muxMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        var opts = new RedisOptions { ConnectionString = "localhost", Database = 0, KeyPrefix = Prefix };
        var client = new RedisClient(muxMock.Object, new JsonRedisSerializer(), opts);
        return (muxMock, dbMock, client);
    }

    [Fact]
    public async Task SetAsync_WithExpiry_CallsStringSetWithPrefixedKeyAndSerializedValue()
    {
        // Arrange — RedisClient.SetAsync 调用 StringSetAsync(key, value, expiry ?? Expiration.Default)
        // expiry(TimeSpan?) 经 ?? 转为 Expiration?，匹配 5 参重载 (RedisKey, RedisValue, Expiration?, ValueCondition?, CommandFlags?)
        var (_, db, client) = Create();
        const string key = "user:1";
        var value = new Sample { Id = 1, Name = "alice" };
        var expiry = TimeSpan.FromMinutes(5);
        db.Setup(d => d.StringSetAsync(
                It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + key),
                It.Is<StackExchange.Redis.RedisValue>(v => v.ToString().Contains("alice")),
                It.IsAny<Expiration>(),
                It.IsAny<ValueCondition>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await client.SetAsync(key, value, expiry);

        // Assert
        result.Should().BeTrue();
        db.Verify(d => d.StringSetAsync(
            It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + key),
            It.IsAny<StackExchange.Redis.RedisValue>(),
            It.IsAny<Expiration>(),
            It.IsAny<ValueCondition>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task GetAsync_KeyExists_ReturnsDeserializedValue()
    {
        // Arrange
        var (_, db, client) = Create();
        const string key = "user:1";
        var json = "{\"id\":1,\"name\":\"bob\"}";
        db.Setup(d => d.StringGetAsync(It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + key), It.IsAny<CommandFlags>()))
            .ReturnsAsync(new StackExchange.Redis.RedisValue(json));

        // Act
        var result = await client.GetAsync<Sample>(key);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("bob");
    }

    [Fact]
    public async Task GetAsync_KeyMissing_ReturnsDefault()
    {
        // Arrange
        var (_, db, client) = Create();
        db.Setup(d => d.StringGetAsync(It.IsAny<StackExchange.Redis.RedisKey>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(StackExchange.Redis.RedisValue.Null);

        // Act
        var result = await client.GetAsync<Sample>("missing");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAsync_CallsKeyDeleteWithPrefixedKey()
    {
        // Arrange
        var (_, db, client) = Create();
        const string key = "user:1";
        db.Setup(d => d.KeyDeleteAsync(It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + key), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await client.RemoveAsync(key);

        // Assert
        result.Should().BeTrue();
        db.Verify(d => d.KeyDeleteAsync(It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + key), It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task ExistsAsync_CallsKeyExistsWithPrefixedKey()
    {
        // Arrange
        var (_, db, client) = Create();
        db.Setup(d => d.KeyExistsAsync(It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + "k"), It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await client.ExistsAsync("k");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IncrementAsync_CallsStringIncrementWithPrefixedKeyAndValue()
    {
        // Arrange
        var (_, db, client) = Create();
        db.Setup(d => d.StringIncrementAsync(
                It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + "counter"),
                It.IsAny<long>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(5L);

        // Act
        var result = await client.IncrementAsync("counter", 2);

        // Assert
        result.Should().Be(5L);
        db.Verify(d => d.StringIncrementAsync(
            It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + "counter"),
            2L,
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SetNxAsync_CallsStringSetWithWhenNotExistsAndPrefixedKey()
    {
        // Arrange — RedisClient.SetNxAsync 调用 StringSetAsync(key, value, expiry, When.NotExists)
        // 4 参重载 (RedisKey, RedisValue, TimeSpan?, When)
        var (_, db, client) = Create();
        var expiry = TimeSpan.FromSeconds(30);
        db.Setup(d => d.StringSetAsync(
                It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + "lock"),
                It.Is<StackExchange.Redis.RedisValue>(v => v.ToString() == "owner-1"),
                expiry,
                When.NotExists))
            .ReturnsAsync(true);

        // Act
        var result = await client.SetNxAsync("lock", "owner-1", expiry);

        // Assert
        result.Should().BeTrue();
        db.Verify(d => d.StringSetAsync(
            It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + "lock"),
            It.IsAny<StackExchange.Redis.RedisValue>(),
            expiry,
            When.NotExists), Times.Once);
    }

    [Fact]
    public async Task ExpireAsync_CallsKeyExpireWithPrefixedKey()
    {
        // Arrange — RedisClient.ExpireAsync 调用 KeyExpireAsync(key, expiry)
        // 解析到 4 参重载 (RedisKey, TimeSpan?, ExpireWhen?, CommandFlags?)，后两参可选
        var (_, db, client) = Create();
        var expiry = TimeSpan.FromMinutes(1);
        db.Setup(d => d.KeyExpireAsync(
                It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + "k"),
                expiry,
                It.IsAny<ExpireWhen>(),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await client.ExpireAsync("k", expiry);

        // Assert
        result.Should().BeTrue();
        db.Verify(d => d.KeyExpireAsync(
            It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + "k"),
            expiry,
            It.IsAny<ExpireWhen>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task LockAsync_CallsLockTakeWithPrefixedKey()
    {
        // Arrange
        var (_, db, client) = Create();
        var expiry = TimeSpan.FromSeconds(10);
        db.Setup(d => d.LockTakeAsync(
                It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + "lock"),
                It.Is<StackExchange.Redis.RedisValue>(v => v.ToString() == "owner"),
                expiry,
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await client.LockAsync("lock", "owner", expiry);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task LockReleaseAsync_CallsLockReleaseWithPrefixedKey()
    {
        // Arrange
        var (_, db, client) = Create();
        db.Setup(d => d.LockReleaseAsync(
                It.Is<StackExchange.Redis.RedisKey>(k => k.ToString() == Prefix + "lock"),
                It.Is<StackExchange.Redis.RedisValue>(v => v.ToString() == "owner"),
                It.IsAny<CommandFlags>()))
            .ReturnsAsync(true);

        // Act
        var result = await client.LockReleaseAsync("lock", "owner");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Ctor_GetDatabase_CalledWithConfiguredDatabaseIndex()
    {
        // Arrange — 验证 Database 索引透传
        var dbMock = new Mock<IDatabase>();
        var muxMock = new Mock<IConnectionMultiplexer>();
        muxMock.Setup(m => m.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        var opts = new RedisOptions { ConnectionString = "localhost", Database = 7, KeyPrefix = Prefix };

        // Act
        _ = new RedisClient(muxMock.Object, new JsonRedisSerializer(), opts);

        // Assert
        muxMock.Verify(m => m.GetDatabase(7, It.IsAny<object>()), Times.Once);
    }

    private sealed class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}

