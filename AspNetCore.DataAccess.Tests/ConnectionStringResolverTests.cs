using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Internal;

using Moq;

namespace AspNetCore.DataAccess.Tests;

/// <summary>
/// ConnectionStringResolver 单元测试：mock IConfiguration（GetSection/索引器）。
/// 分支：空名→Provider.ToString()，找到→返回串，找不到→抛 InvalidOperationException。
/// 类位于 Internal 命名空间但为 public，可直接访问。
/// 注：GetConnectionString 扩展方法实际读 GetSection("ConnectionStrings")[name]。
/// </summary>
public class ConnectionStringResolverTests
{
    /// <summary>构造 mock IConfiguration，ConnectionStrings 节下放指定键值。</summary>
    private static IConfiguration BuildConfig(params (string key, string? value)[] connStrings)
    {
        var map = connStrings.ToDictionary(x => x.key, x => x.value, StringComparer.OrdinalIgnoreCase);

        var sectionMock = new Mock<IConfigurationSection>();
        // GetConnectionString 用 GetSection("ConnectionStrings")[name]
        sectionMock.Setup(s => s[It.IsAny<string>()])
            .Returns<string>(name => map.TryGetValue(name, out var v) ? v : null);
        sectionMock.SetupGet(s => s.Key).Returns("ConnectionStrings");
        sectionMock.SetupGet(s => s.Path).Returns("ConnectionStrings");
        sectionMock.SetupGet(s => s.Value).Returns((string?)null);

        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c.GetSection("ConnectionStrings")).Returns(sectionMock.Object);
        configMock.Setup(c => c.GetSection(It.Is<string>(s => s != "ConnectionStrings")))
            .Returns(new Mock<IConfigurationSection>().Object);
        return configMock.Object;
    }

    [Fact]
    public void ResolveConnectionString_WithNameSpecified_ReturnsNamedConnectionString()
    {
        // Arrange
        var resolver = new ConnectionStringResolver();
        var config = BuildConfig(("MyDb", "Server=my;Db=x"));
        var opts = new DatabaseOptions { Provider = DatabaseProvider.SqlServer, ConnectionStringName = "MyDb" };

        // Act
        var result = resolver.ResolveConnectionString(opts, config);

        // Assert
        result.Should().Be("Server=my;Db=x");
    }

    [Fact]
    public void ResolveConnectionString_WithNullName_FallsBackToProviderName()
    {
        // Arrange — 空名用 Provider.ToString()=SqlServer 作连接串名
        var resolver = new ConnectionStringResolver();
        var config = BuildConfig(("SqlServer", "Server=fallback;Db=y"));
        var opts = new DatabaseOptions { Provider = DatabaseProvider.SqlServer, ConnectionStringName = null };

        // Act
        var result = resolver.ResolveConnectionString(opts, config);

        // Assert
        result.Should().Be("Server=fallback;Db=y");
    }

    [Fact]
    public void ResolveConnectionString_WithWhitespaceName_FallsBackToProviderName()
    {
        // Arrange
        var resolver = new ConnectionStringResolver();
        var config = BuildConfig(("PostgreSql", "Host=pg;Db=z"));
        var opts = new DatabaseOptions { Provider = DatabaseProvider.PostgreSql, ConnectionStringName = "   " };

        // Act
        var result = resolver.ResolveConnectionString(opts, config);

        // Assert
        result.Should().Be("Host=pg;Db=z");
    }

    [Fact]
    public void ResolveConnectionString_WithPostgreSqlProvider_FallsBackToPostgreSqlName()
    {
        // Arrange — Provider=PostgreSql 时用 "PostgreSql" 作名
        var resolver = new ConnectionStringResolver();
        var config = BuildConfig(("PostgreSql", "Host=pg2;Db=z2"));
        var opts = new DatabaseOptions { Provider = DatabaseProvider.PostgreSql, ConnectionStringName = null };

        // Act
        var result = resolver.ResolveConnectionString(opts, config);

        // Assert
        result.Should().Be("Host=pg2;Db=z2");
    }

    [Fact]
    public void ResolveConnectionString_WhenNotFound_ThrowsInvalidOperationException()
    {
        // Arrange — 配置中无对应连接串
        var resolver = new ConnectionStringResolver();
        var config = BuildConfig(("Other", "Server=o"));
        var opts = new DatabaseOptions { Provider = DatabaseProvider.SqlServer, ConnectionStringName = "Missing" };

        // Act
        var act = () => resolver.ResolveConnectionString(opts, config);

        // Assert
        var ex = act.Should().Throw<InvalidOperationException>();
        ex.Which.Message.Should().Contain("Missing");
    }

    [Fact]
    public void ResolveConnectionString_WhenEmptyValue_ThrowsInvalidOperationException()
    {
        // Arrange — 连接串值为空白
        var resolver = new ConnectionStringResolver();
        var config = BuildConfig(("Empty", "   "));
        var opts = new DatabaseOptions { Provider = DatabaseProvider.SqlServer, ConnectionStringName = "Empty" };

        // Act
        var act = () => resolver.ResolveConnectionString(opts, config);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ResolveConnectionString_WhenNotFoundWithNullName_MessageContainsProviderName()
    {
        // Arrange — 错误信息应包含实际解析用的连接串名
        var resolver = new ConnectionStringResolver();
        var config = BuildConfig();
        var opts = new DatabaseOptions { Provider = DatabaseProvider.PostgreSql, ConnectionStringName = null };

        // Act
        var act = () => resolver.ResolveConnectionString(opts, config);

        // Assert
        var ex = act.Should().Throw<InvalidOperationException>();
        ex.Which.Message.Should().Contain("PostgreSql");
    }
}
