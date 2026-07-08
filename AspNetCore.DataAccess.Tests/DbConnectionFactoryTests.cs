using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Abstractions;
using AspNetCore.DataAccess.Internal;

using Moq;

namespace AspNetCore.DataAccess.Tests;

/// <summary>
/// DbConnectionFactory 单元测试：mock IConnectionStringResolver + IOptions。
/// 分支：SqlServer→SqlConnection，PostgreSql→NpgsqlConnection，非法→NotSupportedException。
/// 注：真实连接对象构造不连库（Open 才连），可安全创建。
/// </summary>
public class DbConnectionFactoryTests
{
    private static DbConnectionFactory Create(
        DatabaseProvider provider,
        string connectionString,
        string? connectionStringName = null)
    {
        var opts = new DatabaseOptions
        {
            Provider = provider,
            ConnectionStringName = connectionStringName,
            Orm = OrmType.Dapper
        };
        var optionsMock = new Mock<IOptions<DatabaseOptions>>();
        optionsMock.Setup(o => o.Value).Returns(opts);

        // mock IConfiguration（工厂本身不直接读，resolver 读，这里给空 mock）
        var configMock = new Mock<IConfiguration>();

        var resolverMock = new Mock<IConnectionStringResolver>();
        resolverMock.Setup(r => r.ResolveConnectionString(It.IsAny<DatabaseOptions>(), It.IsAny<IConfiguration>()))
            .Returns(connectionString);

        return new DbConnectionFactory(optionsMock.Object, configMock.Object, resolverMock.Object);
    }

    [Fact]
    public void CreateConnection_WithSqlServer_ReturnsSqlConnection()
    {
        // Arrange
        var factory = Create(DatabaseProvider.SqlServer, "Server=localhost;Database=test;Trusted_Connection=True;");

        // Act
        var conn = factory.CreateConnection();

        // Assert — 不 Open，仅验证类型与连接串
        conn.Should().NotBeNull();
        conn.GetType().Name.Should().Be("SqlConnection");
        conn.ConnectionString.Should().Contain("Server=localhost");
    }

    [Fact]
    public void CreateConnection_WithPostgreSql_ReturnsNpgsqlConnection()
    {
        // Arrange
        var factory = Create(DatabaseProvider.PostgreSql, "Host=localhost;Database=test");

        // Act
        var conn = factory.CreateConnection();

        // Assert
        conn.Should().NotBeNull();
        conn.GetType().Name.Should().Be("NpgsqlConnection");
        conn.ConnectionString.Should().Contain("Host=localhost");
    }

    [Fact]
    public void CreateConnection_ReturnsClosedConnection()
    {
        // Arrange — 工厂只创建不打开
        var factory = Create(DatabaseProvider.SqlServer, "Server=localhost;Database=test;Trusted_Connection=True;");

        // Act
        var conn = factory.CreateConnection();

        // Assert
        conn.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public void CreateConnection_ReturnsIDbConnection()
    {
        // Arrange — 实现 IDbConnectionFactory
        var factory = Create(DatabaseProvider.PostgreSql, "Host=localhost;Database=test");

        // Act
        var conn = factory.CreateConnection();

        // Assert
        conn.Should().BeAssignableTo<IDbConnection>();
    }

    [Fact]
    public void CreateConnection_CallsResolverWithOptionsAndConfiguration()
    {
        // Arrange — 用独立 mock 验证调用
        var opts = new DatabaseOptions { Provider = DatabaseProvider.SqlServer, ConnectionStringName = "MyCs" };
        var optionsMock = new Mock<IOptions<DatabaseOptions>>();
        optionsMock.Setup(o => o.Value).Returns(opts);
        var config = new Mock<IConfiguration>().Object;
        var resolverMock = new Mock<IConnectionStringResolver>();
        resolverMock.Setup(r => r.ResolveConnectionString(opts, config)).Returns("Server=x");
        var factory = new DbConnectionFactory(optionsMock.Object, config, resolverMock.Object);

        // Act
        factory.CreateConnection();

        // Assert
        resolverMock.Verify(r => r.ResolveConnectionString(opts, config), Times.Once);
    }

    [Fact]
    public void Ctor_StoresOptionsValueFromIOptions()
    {
        // Arrange — 验证 IOptions.Value 被解包
        var opts = new DatabaseOptions { Provider = DatabaseProvider.SqlServer };
        var optionsMock = new Mock<IOptions<DatabaseOptions>>();
        optionsMock.Setup(o => o.Value).Returns(opts);
        var resolverMock = new Mock<IConnectionStringResolver>();
        resolverMock.Setup(r => r.ResolveConnectionString(It.IsAny<DatabaseOptions>(), It.IsAny<IConfiguration>()))
            .Returns("Server=x");

        var factory = new DbConnectionFactory(optionsMock.Object, new Mock<IConfiguration>().Object, resolverMock.Object);

        // Act
        factory.CreateConnection();

        // Assert
        optionsMock.Verify(o => o.Value, Times.AtLeastOnce);
    }
}
