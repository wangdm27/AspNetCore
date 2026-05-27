using System.Data;

namespace AspNetCore.DataAccess.Abstractions
{
    /// <summary>
    /// 数据库连接工厂接口
    /// </summary>
    /// <remarks>
    /// 提供创建数据库连接的抽象工厂方法，支持依赖注入和连接池管理
    /// </remarks>
    public interface IDbConnectionFactory
    {
        /// <summary>
        /// 创建数据库连接
        /// </summary>
        /// <returns>数据库连接实例</returns>
        IDbConnection CreateConnection();
    }
}