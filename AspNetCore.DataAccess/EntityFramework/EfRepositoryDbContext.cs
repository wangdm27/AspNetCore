using Microsoft.EntityFrameworkCore;

namespace AspNetCore.DataAccess.EntityFramework
{
    /// <summary>
    /// 基于 Entity Framework Core 的仓储数据库上下文实现，封装 DbContext 操作
    /// </summary>
    /// <typeparam name="TDbContext">Entity Framework Core 数据库上下文类型</typeparam>
    public sealed class EfRepositoryDbContext<TDbContext> : IRepositoryDbContext
        where TDbContext : DbContext
    {
        private readonly TDbContext _dbContext;

        /// <summary>
        /// 初始化 EfRepositoryDbContext 的新实例
        /// </summary>
        /// <param name="dbContext">Entity Framework Core 数据库上下文</param>
        public EfRepositoryDbContext(TDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 获取指定实体类型的 DbSet
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <returns>指定实体类型的 DbSet</returns>
        public DbSet<TEntity> Set<TEntity>() where TEntity : class
        {
            return _dbContext.Set<TEntity>();
        }

        /// <summary>
        /// 根据主键值异步查找实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <param name="keyValues">主键值数组</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>找到的实体，如果不存在则返回 null</returns>
        public ValueTask<TEntity?> FindAsync<TEntity>(object[] keyValues, CancellationToken cancellationToken = default)
            where TEntity : class
        {
            return _dbContext.Set<TEntity>().FindAsync(keyValues, cancellationToken);
        }

        /// <summary>
        /// 异步添加新实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <param name="entity">要添加的实体</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public Task AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
            where TEntity : class
        {
            return _dbContext.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();
        }

        /// <summary>
        /// 更新现有实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <param name="entity">要更新的实体</param>
        public void Update<TEntity>(TEntity entity) where TEntity : class
        {
            _dbContext.Set<TEntity>().Update(entity);
        }

        /// <summary>
        /// 删除实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <param name="entity">要删除的实体</param>
        public void Remove<TEntity>(TEntity entity) where TEntity : class
        {
            _dbContext.Set<TEntity>().Remove(entity);
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