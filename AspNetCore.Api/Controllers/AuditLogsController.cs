using AspNetCore.Api.Infrastructure.Extensions;
using AspNetCore.Api.Infrastructure.Services;
using AspNetCore.Api.Modules.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCore.Api.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/audit-logs")]
    /// <summary>
    /// 审计日志控制器
    /// 提供审计日志的查询接口，支持多条件筛选和分页查询
    /// </summary>
    public class AuditLogsController : ControllerBase
    {
        /// <summary>
        /// 审计日志服务接口
        /// 用于执行审计日志的查询操作
        /// </summary>
        private readonly IAuditLogService _auditLogService;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="auditLogService">审计日志服务实例</param>
        public AuditLogsController(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// 分页查询审计日志列表
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="entityType">实体类型（可选）</param>
        /// <param name="entityId">实体ID（可选）</param>
        /// <param name="action">操作类型（可选）</param>
        /// <param name="startTime">开始时间（可选）</param>
        /// <param name="endTime">结束时间（可选）</param>
        /// <param name="pageIndex">页码，从1开始</param>
        /// <param name="pageSize">每页数量</param>
        /// <returns>分页审计日志数据</returns>
        [HttpGet]
        [PermissionAuthorize("audit.view")]
        public async Task<ActionResult> GetAsync(
            CancellationToken cancellationToken,
            string? entityType = null,
            Guid? entityId = null,
            string? action = null,
            DateTime? startTime = null,
            DateTime? endTime = null,
            int pageIndex = 1,
            int pageSize = 20)
        {
            var (items, totalCount) = await _auditLogService.QueryAsync(
                tenantId: HttpContext.GetRequiredTenantId(),
                entityType: entityType,
                entityId: entityId,
                action: action,
                startTime: startTime,
                endTime: endTime,
                pageIndex: pageIndex,
                pageSize: pageSize,
                cancellationToken: cancellationToken);

            return Ok(new
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = pageIndex,
                PageSize = pageSize
            });
        }
    }
}