using AspNetCore.DataAccess.Abstractions;

namespace AspNetCore.DataAccess.Dapper
{
    /// <summary>
    /// 基于 Dapper 的工作单元实现，用于管理事务和统一保存数据更改
    /// </summary>
    public sealed class DapperUnitOfWork : IUnitOfWork
    {
        private readonly IDapperContext _dapperContext;

        /// <summary>
        /// 初始化 DapperUnitOfWork 的新实例
        /// </summary>
        /// <param name="dapperContext">Dapper 数据库上下文</param>
        public DapperUnitOfWork(IDapperContext dapperContext)
        {
            _dapperContext = dapperContext;
        }

        /// <summary>
        /// 异步保存所有对数据源的更改，通过提交事务实现
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>固定返回 1，表示操作成功</returns>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _dapperContext.CommitAsync(cancellationToken);
            return 1;
        }
    }
}