using AspNetCore.DataAccess.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AspNetCore.DataAccess.EntityFramework
{
    /// <summary>
    /// 基于 Entity Framework Core 的泛型仓储实现，提供对实体的基本 CRUD 操作
    /// </summary>
    /// <typeparam name="TEntity">实体类型，必须为引用类型</typeparam>
    public sealed class EfRepository<TEntity> : IRepository<TEntity>
        where TEntity : class
    {
        private readonly IRepositoryDbContext _dbContext;

        /// <summary>
        /// 初始化 EfRepository 的新实例
        /// </summary>
        /// <param name="dbContext">仓储数据库上下文</param>
        public EfRepository(IRepositoryDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 根据实体 ID 异步获取单个实体
        /// </summary>
        /// <param name="id">实体的唯一标识符</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>找到的实体，如果不存在则返回 null</returns>
        public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.FindAsync<TEntity>(new[] { id }, cancellationToken);
        }

        /// <summary>
        /// 异步获取所有实体（不跟踪更改）
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>包含所有实体的只读列表</returns>
        public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<TEntity>().AsNoTracking().ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 根据指定条件异步查找实体集合（不跟踪更改）
        /// </summary>
        /// <param name="predicate">用于筛选实体的表达式条件</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>符合条件实体的只读列表</returns>
        public async Task<IReadOnlyList<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken cancellationToken = default)
        {
            return await _dbContext.Set<TEntity>()
                .AsNoTracking()
                .Where(predicate)
                .ToListAsync(cancellationToken);
        }

        /// <summary>
        /// 异步添加新实体
        /// </summary>
        /// <param name="entity">要添加的实体</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            return _dbContext.AddAsync(entity, cancellationToken);
        }

        /// <summary>
        /// 异步更新现有实体
        /// </summary>
        /// <param name="entity">要更新的实体</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
        {
            _dbContext.Update(entity);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 异步删除指定 ID 的实体
        /// </summary>
        /// <param name="id">要删除实体的唯一标识符</param>
        /// <param name="cancellationToken">取消操作的令牌</param>
        public async Task DeleteAsync(object id, CancellationToken cancellationToken = default)
        {
            var entity = await GetByIdAsync(id, cancellationToken);
            if (entity is null)
            {
                return;
            }

            _dbContext.Remove(entity);
        }
    }
}