namespace AspNetCore.DataAccess
{
    /// <summary>
    /// 数据库提供程序枚举，定义支持的数据库类型
    /// </summary>
    public enum DatabaseProvider
    {
        /// <summary>
        /// Microsoft SQL Server 数据库
        /// </summary>
        SqlServer = 1,

        /// <summary>
        /// PostgreSQL 数据库
        /// </summary>
        PostgreSql = 2
    }
}