using AspNetCore.DataAccess.Abstractions;

namespace AspNetCore.DataAccess.EntityFramework
{
    /// <summary>
    /// 基于 Entity Framework Core 的工作单元实现，用于管理事务和统一保存数据更改
    /// </summary>
    public sealed class EfUnitOfWork : IUnitOfWork
    {
        private readonly IRepositoryDbContext _dbContext;

        /// <summary>
        /// 初始化 EfUnitOfWork 的新实例
        /// </summary>
        /// <param name="dbContext">仓储数据库上下文</param>
        public EfUnitOfWork(IRepositoryDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 异步保存所有对数据源的更改
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>受影响的实体数量</returns>
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}