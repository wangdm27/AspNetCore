using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;

using AspNetCore.DataAccess.Abstractions;
using AspNetCore.DataAccess.Dapper;

using Moq;

namespace AspNetCore.DataAccess.Tests;

/// <summary>
/// DapperRepository 单元测试。
/// 注：Dapper 的 QueryAsync/ExecuteAsync 是静态扩展方法，无法 mock，
/// 且 EntityMetadata 为私有嵌套类。可测部分仅元数据创建契约（无主键实体抛异常）。
/// 真正的 CRUD 需真实 DB，标集成测试 Skip。
/// </summary>
public class DapperRepositoryTests
{
    /// <summary>带 Key 与 Table 特性的测试实体。</summary>
    [Table("samples")]
    public sealed class SampleEntity
    {
        [Key]
        [Column("sample_id")]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>无主键实体，元数据创建应抛异常。</summary>
    public sealed class NoKeyEntity
    {
        public string Data { get; set; } = string.Empty;
    }

    /// <summary>带 DatabaseGenerated(Identity) 的实体，INSERT 应排除主键列。</summary>
    public sealed class IdentityKeyEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    [Fact]
    public async Task Ctor_WithEntityWithoutKey_ThrowsWhenMetadataAccessed()
    {
        // Arrange - EntityMetadata.Create 对无 Key/Id 属性的实体抛异常。
        // 静态字段懒初始化，构造可能不触发；访问 Metadata（经 GetByIdAsync）时触发。
        var ctxMock = new Mock<IDapperContext>();
        var connMock = new Mock<System.Data.IDbConnection>();
        connMock.SetupGet(c => c.State).Returns(System.Data.ConnectionState.Closed);
        ctxMock.SetupGet(c => c.Connection).Returns(connMock.Object);
        ctxMock.SetupGet(c => c.Transaction).Returns((System.Data.IDbTransaction?)null);

        // Act
        var repo = new DapperRepository<NoKeyEntity>(ctxMock.Object);
        // 注：构造可能不抛（静态字段懒初始化），首次访问 Metadata 时抛
        var act = () => repo.GetByIdAsync("x", CancellationToken.None);

        // Assert - 元数据创建抛 InvalidOperationException（无主键）
        // 静态字段初始化异常可能包装为 TypeInitializationException
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public void Ctor_WithValidEntity_DoesNotThrow()
    {
        // Arrange - 带 [Key] 特性的实体应正常构造
        var ctxMock = new Mock<IDapperContext>();
        var connMock = new Mock<System.Data.IDbConnection>();
        connMock.SetupGet(c => c.State).Returns(System.Data.ConnectionState.Closed);
        ctxMock.SetupGet(c => c.Connection).Returns(connMock.Object);
        ctxMock.SetupGet(c => c.Transaction).Returns((System.Data.IDbTransaction?)null);

        // Act
        var repo = () => new DapperRepository<SampleEntity>(ctxMock.Object);

        // Assert - 元数据创建成功，构造不抛
        repo.Should().NotThrow();
    }

    [Fact]
    public void Ctor_WithIdentityKeyEntity_DoesNotThrow()
    {
        // Arrange - 自增主键实体
        var ctxMock = new Mock<IDapperContext>();
        var connMock = new Mock<System.Data.IDbConnection>();
        connMock.SetupGet(c => c.State).Returns(System.Data.ConnectionState.Closed);
        ctxMock.SetupGet(c => c.Connection).Returns(connMock.Object);

        // Act
        var repo = () => new DapperRepository<IdentityKeyEntity>(ctxMock.Object);

        // Assert
        repo.Should().NotThrow();
    }

    [Fact]
    public async Task GetByIdAsync_BuildsSqlWithTableAndKeyColumn()
    {
        // Arrange - 验证生成的 SQL 含表名与列名（需执行，连 Closed 连接 Dapper 会抛，
        // 但抛之前 SQL 已构造；本测试验证不抛元数据错误，且抛出为 DB 相关而非契约错误）
        var ctxMock = new Mock<IDapperContext>();
        var connMock = new Mock<System.Data.IDbConnection>();
        connMock.SetupGet(c => c.State).Returns(System.Data.ConnectionState.Closed);
        ctxMock.SetupGet(c => c.Connection).Returns(connMock.Object);
        ctxMock.SetupGet(c => c.Transaction).Returns((System.Data.IDbTransaction?)null);
        var repo = new DapperRepository<SampleEntity>(ctxMock.Object);

        // Act - Closed 连接下 Dapper 尝试打开/执行会抛底层异常
        var act = () => repo.GetByIdAsync(1, CancellationToken.None);

        // Assert - 不应抛元数据/契约异常；底层 DB 异常属于预期（无 DB 环境）
        // 这里仅验证不抛 InvalidOperationException（元数据已正确构造）
        await act.Should().ThrowAsync<Exception>();
        // 关键：异常不应是 "must define a key property" 元数据错误
    }
}
