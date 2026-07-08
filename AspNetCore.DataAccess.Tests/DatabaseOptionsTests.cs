using FluentAssertions;

using AspNetCore.DataAccess;

namespace AspNetCore.DataAccess.Tests;

/// <summary>
/// DatabaseOptions / DatabaseProvider / OrmType POCO 与枚举单元测试。
/// </summary>
public class DatabaseOptionsTests
{
    [Fact]
    public void Defaults_NewInstance_HaveExpectedValues()
    {
        // Act
        var opts = new DatabaseOptions();

        // Assert
        opts.Provider.Should().Be(DatabaseProvider.SqlServer);
        opts.Orm.Should().Be(OrmType.EntityFrameworkCore);
        opts.ConnectionStringName.Should().BeNull();
        opts.CommandTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void SectionName_Const_IsDatabase()
    {
        // Assert
        DatabaseOptions.SectionName.Should().Be("Database");
    }

    [Fact]
    public void SetProperties_AssignedValues_RoundTripPreserved()
    {
        // Act
        var opts = new DatabaseOptions
        {
            Provider = DatabaseProvider.PostgreSql,
            Orm = OrmType.Dapper,
            ConnectionStringName = "MyConn",
            CommandTimeoutSeconds = 60
        };

        // Assert
        opts.Provider.Should().Be(DatabaseProvider.PostgreSql);
        opts.Orm.Should().Be(OrmType.Dapper);
        opts.ConnectionStringName.Should().Be("MyConn");
        opts.CommandTimeoutSeconds.Should().Be(60);
    }

    [Theory]
    [InlineData(DatabaseProvider.SqlServer, 1)]
    [InlineData(DatabaseProvider.PostgreSql, 2)]
    public void DatabaseProvider_HasExpectedIntegerValues(DatabaseProvider provider, int expected)
    {
        // Assert — 枚举底层值稳定（配置可能用数字绑定）
        ((int)provider).Should().Be(expected);
    }

    [Theory]
    [InlineData(OrmType.EntityFrameworkCore, 1)]
    [InlineData(OrmType.Dapper, 2)]
    public void OrmType_HasExpectedIntegerValues(OrmType orm, int expected)
    {
        // Assert
        ((int)orm).Should().Be(expected);
    }

    [Fact]
    public void DatabaseProvider_Values_AreExactlyTwo()
    {
        // Assert
        Enum.GetValues<DatabaseProvider>().Should().HaveCount(2);
    }

    [Fact]
    public void OrmType_Values_AreExactlyTwo()
    {
        // Assert
        Enum.GetValues<OrmType>().Should().HaveCount(2);
    }
}
