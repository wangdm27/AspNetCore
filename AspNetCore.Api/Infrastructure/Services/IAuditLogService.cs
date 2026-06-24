using AspNetCore.DataAccess.Entities;

namespace AspNetCore.Api.Infrastructure.Services
{
    /// <summary>
    /// 审计日志服务接口
    /// </summary>
    public interface IAuditLogService
    {
        /// <summary>
        /// 记录审计日志
        /// </summary>
        Task LogAsync(
            string action,
            string entityType,
            Guid? entityId = null,
            string details = "",
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 查询审计日志（分页）
        /// </summary>
        Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> QueryAsync(
            Guid? tenantId = null,
            string? entityType = null,
            Guid? entityId = null,
            string? action = null,
            DateTime? startTime = null,
            DateTime? endTime = null,
            int pageIndex = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default);
    }
}
