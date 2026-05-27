namespace AspNetCore.DataAccess
{
    /// <summary>
    /// 数据库配置选项类，用于配置数据库访问相关参数
    /// </summary>
    public sealed class DatabaseOptions
    {
        /// <summary>
        /// 配置节名称，用于从配置文件中读取数据库配置
        /// </summary>
        public const string SectionName = "Database";

        /// <summary>
        /// 获取或设置数据库提供程序，默认为 SQL Server
        /// </summary>
        public DatabaseProvider Provider { get; set; } = DatabaseProvider.SqlServer;

        /// <summary>
        /// 获取或设置 ORM 类型，默认为 Entity Framework Core
        /// </summary>
        public OrmType Orm { get; set; } = OrmType.EntityFrameworkCore;

        /// <summary>
        /// 获取或设置连接字符串名称，如果未设置则使用 Provider 的值作为名称
        /// </summary>
        public string? ConnectionStringName { get; set; }

        /// <summary>
        /// 获取或设置数据库命令超时时间（秒），默认为 30 秒
        /// </summary>
        public int CommandTimeoutSeconds { get; set; } = 30;
    }
}