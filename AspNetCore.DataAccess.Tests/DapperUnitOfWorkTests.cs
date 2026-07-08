using System.Data;
using FluentAssertions;

using AspNetCore.DataAccess.Abstractions;
using AspNetCore.DataAccess.Dapper;

using Moq;

namespace AspNetCore.DataAccess.Tests;

/// <summary>
/// DapperUnitOfWork 单元测试：mock IDapperContext。
/// SaveChangesAsync 委托 CommitAsync 并返回 1。
/// </summary>
public class DapperUnitOfWorkTests
{
    [Fact]
    public async Task SaveChangesAsync_CallsCommitOnContext()
    {
        // Arrange
        var ctxMock = new Mock<IDapperContext>();
        var uow = new DapperUnitOfWork(ctxMock.Object);

        // Act
        var result = await uow.SaveChangesAsync();

        // Assert
        result.Should().Be(1);
        ctxMock.Verify(c => c.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveChangesAsync_ReturnsOne()
    {
        // Arrange — 固定返回 1 表示成功
        var ctxMock = new Mock<IDapperContext>();
        var uow = new DapperUnitOfWork(ctxMock.Object);

        // Act
        var result = await uow.SaveChangesAsync(CancellationToken.None);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task SaveChangesAsync_WithCancellationToken_PassesTokenToCommit()
    {
        // Arrange
        var ctxMock = new Mock<IDapperContext>();
        var uow = new DapperUnitOfWork(ctxMock.Object);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Act
        await uow.SaveChangesAsync(token);

        // Assert
        ctxMock.Verify(c => c.CommitAsync(token), Times.Once);
    }
}
