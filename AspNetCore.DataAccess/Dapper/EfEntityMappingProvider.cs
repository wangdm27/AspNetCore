using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AspNetCore.DataAccess.Dapper
{
    /// <summary>
    /// 基于 EF Core 模型的实体映射信息提供者：以数据库上下文的模型作为 Dapper 的映射总账，
    /// 保证 Dapper 与 EF Core 共用同一份映射配置
    /// </summary>
    /// <typeparam name="TDbContext">EF Core 数据库上下文类型</typeparam>
    public sealed class EfEntityMappingProvider<TDbContext> : IEntityMappingProvider
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;

        /// <summary>
        /// 初始化 EfEntityMappingProvider 的新实例
        /// </summary>
        /// <param name="dbContext">EF Core 数据库上下文（仅读取其模型元数据，不访问数据库）</param>
        public EfEntityMappingProvider(TDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 获取 EF Core 元数据模型
        /// </summary>
        /// <returns>EF Core 元数据模型</returns>
        public IModel GetModel() => _dbContext.Model;
    }
}
