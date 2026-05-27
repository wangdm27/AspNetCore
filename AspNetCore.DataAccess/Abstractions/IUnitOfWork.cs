namespace AspNetCore.DataAccess.Abstractions
{
    /// <summary>
    /// 工作单元接口，用于管理事务和统一保存数据更改
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// 异步保存所有对数据源的更改
        /// </summary>
        /// <param name="cancellationToken">取消操作的令牌</param>
        /// <returns>受影响的实体数量</returns>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}