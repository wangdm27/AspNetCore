using FluentAssertions;

using AspNetCore.RabbitMq;

namespace AspNetCore.RabbitMq.Tests;

/// <summary>
/// RabbitMqPublishNackedException 单元测试：构造函数字段与消息格式。
/// </summary>
public class RabbitMqPublishNackedExceptionTests
{
    [Fact]
    public void Ctor_WithSequenceNumber_SetsPropertyAndMessage()
    {
        // Arrange
        const ulong seq = 42UL;

        // Act
        var ex = new RabbitMqPublishNackedException(seq);

        // Assert
        ex.PublishSequenceNumber.Should().Be(seq);
        ex.Message.Should().Contain("42");
        ex.Message.Should().Contain("nacked");
    }

    [Fact]
    public void Ctor_WithZeroSequence_SetsProperty()
    {
        // Act
        var ex = new RabbitMqPublishNackedException(0UL);

        // Assert
        ex.PublishSequenceNumber.Should().Be(0UL);
    }

    [Fact]
    public void Ctor_IsExceptionType()
    {
        // Act
        var ex = new RabbitMqPublishNackedException(7UL);

        // Assert
        ex.Should().BeAssignableTo<Exception>();
    }
}
