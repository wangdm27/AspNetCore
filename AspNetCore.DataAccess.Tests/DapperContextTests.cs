using System.Data;
using FluentAssertions;

using AspNetCore.DataAccess.Abstractions;
using AspNetCore.DataAccess.Dapper;

using Moq;

namespace AspNetCore.DataAccess.Tests;

/// <summary>
/// DapperContext 单元测试：mock IDbConnectionFactory + IDbConnection/IDbTransaction。
/// 验证连接/事务生命周期（Begin/Commit/Rollback/Dispose）。
/// </summary>
public class DapperContextTests
{
    private static (Mock<IDbConnectionFactory> factory, Mock<IDbConnection> conn, DapperContext ctx) Create()
    {
        var connMock = new Mock<IDbConnection>();
        connMock.SetupGet(c => c.State).Returns(ConnectionState.Open);
        var factoryMock = new Mock<IDbConnectionFactory>();
        factoryMock.Setup(f => f.CreateConnection()).Returns(connMock.Object);
        var ctx = new DapperContext(factoryMock.Object);
        return (factoryMock, connMock, ctx);
    }

    [Fact]
    public void Ctor_CreatesConnectionViaFactory()
    {
        // Arrange
        var connMock = new Mock<IDbConnection>();
        var factoryMock = new Mock<IDbConnectionFactory>();
        factoryMock.Setup(f => f.CreateConnection()).Returns(connMock.Object);

        // Act
        var ctx = new DapperContext(factoryMock.Object);

        // Assert
        ctx.Connection.Should().BeSameAs(connMock.Object);
        factoryMock.Verify(f => f.CreateConnection(), Times.Once);
        ctx.Transaction.Should().BeNull();
    }

    [Fact]
    public async Task BeginTransactionAsync_WhenConnectionOpen_StartsTransaction()
    {
        // Arrange
        var (factory, conn, ctx) = Create();
        var txMock = new Mock<IDbConnection>();
        var tranMock = new Mock<IDbTransaction>();
        conn.Setup(c => c.BeginTransaction()).Returns(tranMock.Object);

        // Act
        await ctx.BeginTransactionAsync();

        // Assert
        ctx.Transaction.Should().BeSameAs(tranMock.Object);
        conn.Verify(c => c.BeginTransaction(), Times.Once);
    }

    [Fact]
    public async Task BeginTransactionAsync_WhenAlreadyInTransaction_DoesNotStartSecond()
    {
        // Arrange — 已有事务时不应再开
        var (factory, conn, ctx) = Create();
        var tranMock = new Mock<IDbTransaction>();
        conn.Setup(c => c.BeginTransaction()).Returns(tranMock.Object);
        await ctx.BeginTransactionAsync();

        // Act
        await ctx.BeginTransactionAsync();

        // Assert
        ctx.Transaction.Should().BeSameAs(tranMock.Object);
        conn.Verify(c => c.BeginTransaction(), Times.Once);
    }

    [Fact]
    public async Task CommitAsync_CommitsAndDisposesTransaction()
    {
        // Arrange
        var (factory, conn, ctx) = Create();
        var tranMock = new Mock<IDbTransaction>();
        conn.Setup(c => c.BeginTransaction()).Returns(tranMock.Object);
        await ctx.BeginTransactionAsync();

        // Act
        await ctx.CommitAsync();

        // Assert
        tranMock.Verify(t => t.Commit(), Times.Once);
        tranMock.Verify(t => t.Dispose(), Times.Once);
        ctx.Transaction.Should().BeNull();
    }

    [Fact]
    public async Task CommitAsync_WithNoTransaction_DoesNotThrow()
    {
        // Arrange — 无事务时空提交
        var (factory, conn, ctx) = Create();

        // Act
        var act = () => ctx.CommitAsync();

        // Assert
        await act.Should().NotThrowAsync();
        ctx.Transaction.Should().BeNull();
    }

    [Fact]
    public async Task RollbackAsync_RollbacksAndDisposesTransaction()
    {
        // Arrange
        var (factory, conn, ctx) = Create();
        var tranMock = new Mock<IDbTransaction>();
        conn.Setup(c => c.BeginTransaction()).Returns(tranMock.Object);
        await ctx.BeginTransactionAsync();

        // Act
        await ctx.RollbackAsync();

        // Assert
        tranMock.Verify(t => t.Rollback(), Times.Once);
        tranMock.Verify(t => t.Dispose(), Times.Once);
        ctx.Transaction.Should().BeNull();
    }

    [Fact]
    public async Task RollbackAsync_WithNoTransaction_DoesNotThrow()
    {
        // Arrange
        var (factory, conn, ctx) = Create();

        // Act
        var act = () => ctx.RollbackAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_DisposesConnectionAndTransaction()
    {
        // Arrange
        var (factory, conn, ctx) = Create();
        var tranMock = new Mock<IDbTransaction>();
        conn.Setup(c => c.BeginTransaction()).Returns(tranMock.Object);
        ctx.BeginTransactionAsync().Wait();

        // Act
        ctx.Dispose();

        // Assert
        tranMock.Verify(t => t.Dispose(), Times.Once);
        conn.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public void Dispose_CalledTwice_DoesNotDisposeConnectionTwice()
    {
        // Arrange — 幂等
        var (factory, conn, ctx) = Create();

        // Act
        ctx.Dispose();
        ctx.Dispose();

        // Assert
        conn.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task DisposeAsync_DisposesConnectionAndTransaction()
    {
        // Arrange
        var (factory, conn, ctx) = Create();
        var tranMock = new Mock<IDbTransaction>();
        conn.Setup(c => c.BeginTransaction()).Returns(tranMock.Object);
        await ctx.BeginTransactionAsync();

        // Act
        await ctx.DisposeAsync();

        // Assert
        tranMock.Verify(t => t.Dispose(), Times.Once);
        conn.Verify(c => c.Dispose(), Times.Once);
    }
}
