using FluentAssertions;

using AspNetCore.Redis;

namespace AspNetCore.Redis.Tests;

/// <summary>
/// RedisKey 键生成器单元测试。纯静态逻辑，无依赖。
/// </summary>
public class RedisKeyTests
{
    [Fact]
    public void Build_WithPrefixAndKey_ReturnsConcatenated()
    {
        // Arrange
        const string prefix = "app:";
        const string key = "user:1";

        // Act
        var actual = RedisKey.Build(prefix, key);

        // Assert
        actual.Should().Be("app:user:1");
    }

    [Theory]
    [InlineData("app:", "user:1", "app:user:1")]
    [InlineData("", "k", "k")]
    [InlineData("p:", "", "p:")]
    [InlineData(null, "k", "k")]
    [InlineData("p:", null, "p:")]
    [InlineData(null, null, "")]
    public void Build_VariousInputs_ReturnsPrefixPlusKey(string? prefix, string? key, string expected)
    {
        // Act
        var actual = RedisKey.Build(prefix!, key!);

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("123", "user:123")]
    [InlineData("abc-456", "user:abc-456")]
    [InlineData("", "user:")]
    public void User_WithId_ReturnsUserPrefixedKey(string userId, string expected)
    {
        // Act
        var actual = RedisKey.User(userId);

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData("o-1", "order:o-1")]
    [InlineData("", "order:")]
    public void Order_WithId_ReturnsOrderPrefixedKey(string orderId, string expected)
    {
        // Act
        var actual = RedisKey.Order(orderId);

        // Assert
        actual.Should().Be(expected);
    }
}
