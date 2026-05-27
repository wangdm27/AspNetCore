using Microsoft.EntityFrameworkCore;

namespace AspNetCore.DataAccess.EntityFramework
{
    /// <summary>
    /// 仓储数据库上下文接口，提供对实体集合的基本操作
    /// </summary>
    public interface IRepositoryDbContext
    {
        /// <summary>
        /// 获取指定实体类型的 DbSet
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <returns>指定实体类型的 DbSet</returns>
        DbSet<TEntity> Set<TEntity>() where TEntity : class;

        /// <summary>
        /// 根据主键值异步查找实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <param name="keyValues">主键值数组</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>找到的实体，如果不存在则返回 null</returns>
        ValueTask<TEntity?> FindAsync<TEntity>(object[] keyValues, CancellationToken cancellationToken = default)
            where TEntity : class;

        /// <summary>
        /// 异步添加新实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <param name="entity">要添加的实体</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>表示异步操作的任务</returns>
        Task AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = default)
            where TEntity : class;

        /// <summary>
        /// 更新现有实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <param name="entity">要更新的实体</param>
        void Update<TEntity>(TEntity entity) where TEntity : class;

        /// <summary>
        /// 删除实体
        /// </summary>
        /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
        /// <param name="entity">要删除的实体</param>
        void Remove<TEntity>(TEntity entity) where TEntity : class;

        /// <summary>
        /// 异步保存所有对数据源的更改
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>受影响的实体数量</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}