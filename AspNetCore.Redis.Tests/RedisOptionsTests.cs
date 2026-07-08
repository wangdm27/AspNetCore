using FluentAssertions;

using AspNetCore.Redis;

namespace AspNetCore.Redis.Tests;

/// <summary>
/// RedisOptions POCO 默认值与回读单元测试。
/// </summary>
public class RedisOptionsTests
{
    [Fact]
    public void Defaults_NewInstance_HaveExpectedValues()
    {
        // Act
        var opts = new RedisOptions();

        // Assert
        opts.ConnectionString.Should().BeNull();
        opts.Database.Should().Be(0);
        opts.KeyPrefix.Should().Be("app:");
    }

    [Fact]
    public void SetProperties_AssignedValues_RoundTripPreserved()
    {
        // Act
        var opts = new RedisOptions
        {
            ConnectionString = "redis-host:6380",
            Database = 3,
            KeyPrefix = "myapp:"
        };

        // Assert
        opts.ConnectionString.Should().Be("redis-host:6380");
        opts.Database.Should().Be(3);
        opts.KeyPrefix.Should().Be("myapp:");
    }

    [Fact]
    public void KeyPrefix_DefaultAlignsWithRedisKeyBuildContract()
    {
        // Arrange — RedisClient.BuildKey 用 {KeyPrefix}{key}，默认前缀应产生 app:xxx
        var opts = new RedisOptions();

        // Act
        var composed = $"{opts.KeyPrefix}user:1";

        // Assert
        composed.Should().Be("app:user:1");
    }
}
