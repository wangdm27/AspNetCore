using System.Linq.Expressions;

namespace AspNetCore.DataAccess.Abstractions
{
    /// <summary>
    /// 泛型仓储接口，提供对实体的基本 CRUD 操作
    /// </summary>
    /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
    public interface IRepository<TEntity> where TEntity : class
    {
        /// <summary>
        /// 根据实体 ID 异步获取单个实体
        /// </summary>
        /// <param name="id">实体的唯一标识符</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>找到的实体，如果不存在则返回 null</returns>
        Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步获取所有实体
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>包含所有实体的只读列表</returns>
        Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 根据指定条件异步查找实体集合
        /// </summary>
        /// <param name="predicate">用于筛选实体的表达式条件</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>符合条件实体的只读列表</returns>
        Task<IReadOnlyList<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步添加新实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>表示异步操作的任务</returns>
        Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步更新现有实体
        /// </summary>
        /// <param name="entity">要更新的实体</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>表示异步操作的任务</returns>
        Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步删除指定 ID 的实体
        /// </summary>
        /// <param name="id">要删除实体的唯一标识符</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>表示异步操作的任务</returns>
        Task DeleteAsync(object id, CancellationToken cancellationToken = default);
    }
}