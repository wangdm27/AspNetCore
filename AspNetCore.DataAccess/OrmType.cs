namespace AspNetCore.DataAccess
{
    /// <summary>
    /// ORM 类型枚举，定义支持的对象关系映射框架类型
    /// </summary>
    public enum OrmType
    {
        /// <summary>
        /// Entity Framework Core ORM 框架
        /// </summary>
        EntityFrameworkCore = 1,

        /// <summary>
        /// Dapper ORM 框架
        /// </summary>
        Dapper = 2
    }
}