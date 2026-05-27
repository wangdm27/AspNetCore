using AspNetCore.DataAccess.Abstractions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data;

namespace AspNetCore.DataAccess.Internal
{
    /// <summary>
    /// 数据库连接工厂，根据配置创建相应的数据库连接
    /// </summary>
    public sealed class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly DatabaseOptions _options;
        private readonly IConfiguration _configuration;
        private readonly IConnectionStringResolver _connectionStringResolver;

        /// <summary>
        /// 初始化 DbConnectionFactory 的新实例
        /// </summary>
        /// <param name="options">数据库配置选项</param>
        /// <param name="configuration">应用程序配置</param>
        /// <param name="connectionStringResolver">连接字符串解析器</param>
        public DbConnectionFactory(
            IOptions<DatabaseOptions> options,
            IConfiguration configuration,
            IConnectionStringResolver connectionStringResolver)
        {
            _options = options.Value;
            _configuration = configuration;
            _connectionStringResolver = connectionStringResolver;
        }

        /// <summary>
        /// 根据配置创建数据库连接
        /// </summary>
        /// <returns>数据库连接实例</returns>
        /// <exception cref="NotSupportedException">当指定的数据库提供程序不受支持时抛出</exception>
        public IDbConnection CreateConnection()
        {
            var connectionString = _connectionStringResolver.ResolveConnectionString(_options, _configuration);

            return _options.Provider switch
            {
                DatabaseProvider.SqlServer => new SqlConnection(connectionString),
                DatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
                _ => throw new NotSupportedException($"Database provider '{_options.Provider}' is not supported.")
            };
        }
    }
}