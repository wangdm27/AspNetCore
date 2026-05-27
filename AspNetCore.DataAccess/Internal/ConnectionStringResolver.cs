using AspNetCore.DataAccess.Abstractions;
using Microsoft.Extensions.Configuration;

namespace AspNetCore.DataAccess.Internal
{
    /// <summary>
    /// 连接字符串解析器，从配置中解析数据库连接字符串
    /// </summary>
    public sealed class ConnectionStringResolver : IConnectionStringResolver
    {
        /// <summary>
        /// 根据数据库选项解析连接字符串
        /// </summary>
        /// <param name="options">数据库配置选项</param>
        /// <param name="configuration">应用程序配置</param>
        /// <returns>解析后的数据库连接字符串</returns>
        /// <exception cref="InvalidOperationException">当指定的连接字符串未找到时抛出</exception>
        public string ResolveConnectionString(DatabaseOptions options, IConfiguration configuration)
        {
            var connectionStringName = options.ConnectionStringName;
            if (string.IsNullOrWhiteSpace(connectionStringName))
            {
                connectionStringName = options.Provider.ToString();
            }

            var connectionString = configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{connectionStringName}' was not found.");
            }

            return connectionString;
        }
    }
}