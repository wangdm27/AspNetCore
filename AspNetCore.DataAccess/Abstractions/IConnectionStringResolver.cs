using Microsoft.Extensions.Configuration;

namespace AspNetCore.DataAccess.Abstractions
{
    /// <summary>
    /// 数据库连接字符串解析器接口
    /// </summary>
    /// <remarks>
    /// 提供解析和构建数据库连接字符串的功能，支持从数据库配置选项和应用程序配置中组合生成连接字符串
    /// </remarks>
    public interface IConnectionStringResolver
    {
        /// <summary>
        /// 解析数据库连接字符串
        /// </summary>
        /// <param name="options">数据库配置选项</param>
        /// <param name="configuration">应用程序配置</param>
        /// <returns>解析后的数据库连接字符串</returns>
        string ResolveConnectionString(DatabaseOptions options, IConfiguration configuration);
    }
}