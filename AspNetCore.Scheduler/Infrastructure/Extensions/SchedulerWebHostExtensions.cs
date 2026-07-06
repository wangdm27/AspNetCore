using Hangfire;
using Hangfire.Dashboard;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.Scheduler.Infrastructure.Extensions;

public static class SchedulerWebHostExtensions
{
    /// <summary>
    /// 复合 host: 默认 builder (IHostBuilder) 嵌 Kestrel 暴露 Hangfire Dashboard。
    /// </summary>
    public static IHostBuilder ConfigureSchedulerWebHost(this IHostBuilder builder)
    {
        builder.ConfigureWebHostDefaults(web =>
        {
            web.UseUrls("http://localhost:5300");
            web.Configure(app =>
            {
                var cfg = app.ApplicationServices.GetRequiredService<IConfiguration>();
                var hf = cfg.GetSection("Hangfire");
                var path = hf["DashboardPath"] ?? "/hangfire";
                var allowAnon = hf.GetValue<bool?>("DashboardAllowAnonymous") == true;

                app.UseHangfireDashboard(path, new DashboardOptions
                {
                    Authorization = allowAnon
                        ? Array.Empty<IDashboardAuthorizationFilter>()
                        : new[] { new DashboardAuthorizationFilter() }
                });
            });
        });
        return builder;
    }
}
