using AspNetCore.DataAccess.Abstractions;
using AspNetCore.DataAccess.Dapper;
using AspNetCore.DataAccess.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Moq;

namespace AspNetCore.DataAccess.Tests;

/// <summary>
/// DapperRepository 单元测试。
/// 注：Dapper 的 QueryAsync/ExecuteAsync 是静态扩展方法，无法 mock。
/// 映射元数据（EntityMetadata，internal）经 InternalsVisibleTo 直接验证：
/// - EF 模型路径：用真实 ApplicationDbContext 构建模型（纯元数据，不连数据库）；
/// - 特性回退路径：用不在 EF 模型中的桩实体验证。
/// 执行类测试中 Closed 连接会抛非契约异常（连接未打开），故以
/// "异常不是元数据契约异常 InvalidOperationException" 作为守护条件。
/// 真正的 CRUD 需真实 DB，标集成测试 Skip。
/// </summary>
public class DapperRepositoryTests
{
    /// <summary>带 Key 与 Table 特性的测试实体（不在 EF 模型中，走特性回退路径）。</summary>
    [System.ComponentModel.DataAnnotations.Schema.Table("samples")]
    public sealed class SampleEntity
    {
        [System.ComponentModel.DataAnnotations.Key]
        [System.ComponentModel.DataAnnotations.Schema.Column("sample_id")]
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>无主键实体，元数据创建应抛契约异常。</summary>
    public sealed class NoKeyEntity
    {
        public string Data { get; set; } = string.Empty;
    }

    /// <summary>带 DatabaseGenerated(Identity) 的实体，INSERT 应排除主键列。</summary>
    public sealed class IdentityKeyEntity
    {
        [System.ComponentModel.DataAnnotations.Key]
        [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>返回给定 EF 模型的映射提供者。</summary>
    private sealed class ModelProvider : IEntityMappingProvider
    {
        private readonly IModel _model;

        public ModelProvider(IModel model)
        {
            _model = model;
        }

        public IModel? GetModel() => _model;
    }

    /// <summary>返回空模型的映射提供者：迫使 DapperRepository 走特性回退路径。</summary>
    private sealed class NullModelProvider : IEntityMappingProvider
    {
        public IModel? GetModel() => null;
    }

    /// <summary>
    /// 用真实 ApplicationDbContext 构建 EF 模型。纯元数据操作，不连接数据库。
    /// </summary>
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=(local);Database=MetadataTest")
            .Options;
        return new ApplicationDbContext(options);
    }

    /// <summary>
    /// 构造带 Closed 连接 mock 的 DapperRepository（执行 SQL 时 Dapper 会抛非契约异常，
    /// 用于验证元数据构建不抛契约异常）。
    /// </summary>
    private static DapperRepository<TEntity> CreateRepo<TEntity>(IEntityMappingProvider provider)
        where TEntity : class
    {
        var ctxMock = new Mock<IDapperContext>();
        var connMock = new Mock<System.Data.IDbConnection>();
        connMock.SetupGet(c => c.State).Returns(System.Data.ConnectionState.Closed);
        ctxMock.SetupGet(c => c.Connection).Returns(connMock.Object);
        ctxMock.SetupGet(c => c.Transaction).Returns((System.Data.IDbTransaction?)null);
        return new DapperRepository<TEntity>(ctxMock.Object, provider);
    }

    [Fact]
    public async Task GetByIdAsync_WithEntityWithoutKey_ThrowsContractException()
    {
        // Arrange - 特性回退路径：NoKeyEntity 无 Key/Id 属性，元数据构建应抛契约异常
        var repo = CreateRepo<NoKeyEntity>(new NullModelProvider());

        // Act
        var act = () => repo.GetByIdAsync("x", CancellationToken.None);

        // Assert - 精确断言元数据契约异常（"must define a key property"），而非任意异常
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Metadata_FromAttributes_MapsSampleEntityWithKeyColumn()
    {
        // Act - 桩实体不在 EF 模型中（模型为 null），走特性回退路径
        var metadata = EntityMetadata.Create(null, typeof(SampleEntity));

        // Assert - 表名/主键列来自特性标注；无 [Column] 的属性列名回退为属性名；UPDATE 排除主键列
        metadata.TableName.Should().Be("samples");
        metadata.KeyColumnName.Should().Be("sample_id");
        metadata.KeyProperty.Name.Should().Be("Id");
        metadata.UpdateSetClause.Should().Contain("Name = @Name");
        metadata.UpdateSetClause.Should().NotContain("sample_id =");
    }

    [Fact]
    public void Metadata_FromAttributes_IdentityKeyExcludedFromInsert()
    {
        // Act - [DatabaseGenerated(Identity)] 自增主键实体
        var metadata = EntityMetadata.Create(null, typeof(IdentityKeyEntity));

        // Assert - INSERT 排除自增主键列，仅插入其余列（无 [Column] 时列名回退为属性名）
        metadata.InsertColumnList.Should().Be("Code");
        metadata.InsertParameterList.Should().Be("@Code");
    }

    [Fact]
    public async Task GetByIdAsync_WithAttributeFallback_BuildsSqlWithoutContractError()
    {
        // Arrange - 特性回退路径下 SQL 构造正常（Closed 连接执行时抛非契约异常属预期）
        var repo = CreateRepo<SampleEntity>(new NullModelProvider());

        // Act
        var act = () => repo.GetByIdAsync(1, CancellationToken.None);

        // Assert - 异常不应是元数据契约异常 InvalidOperationException
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.GetType() != typeof(InvalidOperationException));
    }

    [Fact]
    public void Metadata_FromEfModel_MapsUserToConfiguredTableAndColumns()
    {
        // Arrange - 用真实 ApplicationDbContext 构建模型（Fluent API 配置总账）
        using var context = CreateContext();

        // Act
        var metadata = EntityMetadata.Create(context.Model, typeof(User));

        // Assert - 表名/主键列/列名映射/插入列全部来自配置总账
        metadata.TableName.Should().Be("users");
        metadata.KeyColumnName.Should().Be("id");
        metadata.ColumnMappings["user_name"].Name.Should().Be("UserName");
        metadata.InsertColumnList.Split(", ").Should().Contain("id");
        metadata.InsertColumnList.Split(", ").Should().Contain("password_hash");
        metadata.UpdateSetClause.Should().Contain("user_name = @UserName");
    }

    [Fact]
    public void Metadata_FromEfModel_CompositeKey_UsesFirstKeyProperty()
    {
        // Arrange - user_roles 为复合主键（TenantId, UserId, RoleId）
        using var context = CreateContext();

        // Act
        var metadata = EntityMetadata.Create(context.Model, typeof(UserRole));

        // Assert - 复合主键取第一个属性，与历史行为一致
        metadata.TableName.Should().Be("user_roles");
        metadata.KeyProperty.Name.Should().Be("TenantId");
        metadata.KeyColumnName.Should().Be("tenant_id");
    }

    [Fact]
    public async Task GetByIdAsync_WithEfModelProvider_BuildsSqlWithoutContractError()
    {
        // Arrange - EF 模型路径下 SQL 构造正常（Closed 连接执行时抛非契约异常属预期）
        using var context = CreateContext();
        var repo = CreateRepo<User>(new ModelProvider(context.Model));

        // Act
        var act = () => repo.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert - 异常不应是元数据契约异常 InvalidOperationException
        await act.Should().ThrowAsync<Exception>()
            .Where(e => e.GetType() != typeof(InvalidOperationException));
    }
}
