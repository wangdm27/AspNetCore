using AspNetCore.Api.Infrastructure.Context;
using AspNetCore.DataAccess;
using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.Api.Infrastructure.Services
{
    /// <summary>
    /// 审计日志服务实现
    /// </summary>
    public sealed class AuditLogService : IAuditLogService
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ICurrentRequestContext _currentRequestContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(
            ApplicationDbContext dbContext,
            ICurrentRequestContext currentRequestContext,
            IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _currentRequestContext = currentRequestContext;
            _httpContextAccessor = httpContextAccessor;
        }

        /// <summary>
        /// 记录审计日志
        /// </summary>
        public async Task LogAsync(
            string action,
            string entityType,
            Guid? entityId = null,
            string details = "",
            CancellationToken cancellationToken = default)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var ipAddress = httpContext?.Connection.RemoteIpAddress?.ToString() ?? "";
            var userAgent = httpContext?.Request.Headers.UserAgent.FirstOrDefault() ?? "";

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = _currentRequestContext.TenantId,
                UserId = _currentRequestContext.UserId,
                UserName = _currentRequestContext.UserName ?? "",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
            // 注意：不在此处 SaveChanges，由调用方的事务统一保存
        }

        /// <summary>
        /// 查询审计日志（分页）
        /// </summary>
        public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> QueryAsync(
            Guid? tenantId = null,
            string? entityType = null,
            Guid? entityId = null,
            string? action = null,
            DateTime? startTime = null,
            DateTime? endTime = null,
            int pageIndex = 1,
            int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var query = _dbContext.AuditLogs.AsNoTracking();

            if (tenantId.HasValue)
            {
                query = query.Where(x => x.TenantId == tenantId.Value);
            }

            if (!string.IsNullOrWhiteSpace(entityType))
            {
                query = query.Where(x => x.EntityType == entityType);
            }

            if (entityId.HasValue)
            {
                query = query.Where(x => x.EntityId == entityId.Value);
            }

            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(x => x.Action == action);
            }

            if (startTime.HasValue)
            {
                query = query.Where(x => x.CreatedAt >= startTime.Value);
            }

            if (endTime.HasValue)
            {
                query = query.Where(x => x.CreatedAt <= endTime.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (items, totalCount);
        }
    }
}
