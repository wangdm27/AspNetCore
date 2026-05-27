using AspNetCore.DataAccess.Abstractions;
using System.Data;

namespace AspNetCore.DataAccess.Dapper
{
    /// <summary>
    /// Dapper 数据库上下文，管理数据库连接和事务
    /// </summary>
    public sealed class DapperContext : IDapperContext
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private bool _disposed;

        /// <summary>
        /// 初始化 DapperContext 的新实例
        /// </summary>
        /// <param name="connectionFactory">数据库连接工厂</param>
        public DapperContext(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
            Connection = _connectionFactory.CreateConnection();
        }

        /// <summary>
        /// 获取数据库连接
        /// </summary>
        public IDbConnection Connection { get; }

        /// <summary>
        /// 获取当前事务，如果没有活动事务则返回 null
        /// </summary>
        public IDbTransaction? Transaction { get; private set; }

        /// <summary>
        /// 异步开始数据库事务
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (Connection.State != ConnectionState.Open && Connection is System.Data.Common.DbConnection dbConnection)
            {
                await dbConnection.OpenAsync(cancellationToken);
            }

            Transaction ??= Connection.BeginTransaction();
        }

        /// <summary>
        /// 异步提交当前事务
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Transaction?.Commit();
            Transaction?.Dispose();
            Transaction = null;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 异步回滚当前事务
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Transaction?.Rollback();
            Transaction?.Dispose();
            Transaction = null;
            return Task.CompletedTask;
        }

        /// <summary>
        /// 释放当前上下文使用的所有资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Transaction?.Dispose();
            Connection.Dispose();
            _disposed = true;
        }

        /// <summary>
        /// 异步释放当前上下文使用的所有资源
        /// </summary>
        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}