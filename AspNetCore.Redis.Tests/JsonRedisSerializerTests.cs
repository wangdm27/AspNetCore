using System.Text.Json;

using FluentAssertions;

using AspNetCore.Redis;

namespace AspNetCore.Redis.Tests;

/// <summary>
/// JsonRedisSerializer 单元测试。基于 System.Text.Json，驼峰命名，无外部依赖。
/// </summary>
public class JsonRedisSerializerTests
{
    private sealed class Sample
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    private readonly JsonRedisSerializer _serializer = new();

    [Fact]
    public void Serialize_Object_ReturnsCamelCaseJson()
    {
        // Arrange
        var sample = new Sample
        {
            Id = 7,
            Name = "alice",
            CreatedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
        };

        // Act
        var json = _serializer.Serialize(sample);

        // Assert
        json.Should().Contain("\"id\":7");
        json.Should().Contain("\"name\":\"alice\"");
        json.Should().Contain("\"createdAt\"");
        json.Should().NotContain("\"Id\":");
        json.Should().NotContain("\"Name\":");
    }

    [Fact]
    public void Serialize_Primitive_ReturnsJsonPrimitive()
    {
        // Act
        var json = _serializer.Serialize(42);

        // Assert
        json.Should().Be("42");
    }

    [Fact]
    public void Serialize_NullReference_ReturnsNullLiteral()
    {
        // Arrange
        object? value = null;

        // Act
        var json = _serializer.Serialize(value);

        // Assert
        json.Should().Be("null");
    }

    [Fact]
    public void Deserialize_ValidJson_ReturnsObject()
    {
        // Arrange
        const string json = "{\"id\":1,\"name\":\"bob\"}";

        // Act
        var result = _serializer.Deserialize<Sample>(json);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(1);
        result.Name.Should().Be("bob");
    }

    [Fact]
    public void Deserialize_NullLiteral_ReturnsNull()
    {
        // Arrange
        const string json = "null";

        // Act
        var result = _serializer.Deserialize<Sample>(json);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Serialize_Deserialize_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new Sample
        {
            Id = 99,
            Name = "round-trip",
            CreatedAt = new DateTime(2026, 7, 8, 0, 0, 0, DateTimeKind.Utc)
        };

        // Act
        var json = _serializer.Serialize(original);
        var restored = _serializer.Deserialize<Sample>(json);

        // Assert
        restored.Should().NotBeNull();
        restored!.Id.Should().Be(original.Id);
        restored.Name.Should().Be(original.Name);
        restored.CreatedAt.Should().Be(original.CreatedAt);
    }

    [Fact]
    public void Deserialize_InvalidJson_ThrowsJsonException()
    {
        // Arrange
        const string invalid = "{not json";

        // Act
        var act = () => _serializer.Deserialize<Sample>(invalid);

        // Assert - 契约：反序列化失败抛 JsonException（IRedisSerializer 文档已与实现对齐）
        act.Should().Throw<JsonException>();
    }
}
