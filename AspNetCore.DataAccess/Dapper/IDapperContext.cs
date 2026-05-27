using System.Data;

namespace AspNetCore.DataAccess.Dapper
{
    /// <summary>
    /// Dapper 数据库上下文接口，管理数据库连接和事务
    /// </summary>
    public interface IDapperContext : IAsyncDisposable, IDisposable
    {
        /// <summary>
        /// 获取数据库连接
        /// </summary>
        IDbConnection Connection { get; }

        /// <summary>
        /// 获取当前事务，如果没有活动事务则返回 null
        /// </summary>
        IDbTransaction? Transaction { get; }

        /// <summary>
        /// 异步开始数据库事务
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        Task BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步提交当前事务
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步回滚当前事务
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}