using Hangfire.Dashboard;

namespace AspNetCore.Scheduler.Infrastructure.Extensions;

/// <summary>
/// Dashboard 授权过滤器占位。
/// 开发环境: DashboardAllowAnonymous=true 时跳过此过滤器。
/// 生产环境: 置 false 后接实际授权 (JWT claims / 固定白名单)。
/// </summary>
public class DashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // 生产授权点。默认拒绝。
        return false;
    }
}
