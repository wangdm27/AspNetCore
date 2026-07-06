using AspNetCore.Scheduler.Jobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.Scheduler.Infrastructure.Extensions;

public static class SchedulerServiceExtensions
{
    /// <summary>
    /// 注册 Hangfire (PostgreSQL 存储) + server worker 池 + Jobs。
    /// </summary>
    public static IHostBuilder AddSchedulerHangfire(this IHostBuilder builder)
    {
        builder.ConfigureServices((ctx, services) =>
        {
            var cfg = ctx.Configuration;
            var hf = cfg.GetSection("Hangfire");
            var conn = cfg.GetConnectionString("HangfirePostgreSql")
                ?? throw new InvalidOperationException("HangfirePostgreSql 连接串缺失");

            // 全局限重试 3 次 (覆盖 Hangfire 默认 10 次)
            GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 3 });

            services.AddHangfire(c => c
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(conn)));

            services.AddHangfireServer(opt =>
            {
                opt.Queues = new[] { hf["QueueName"] ?? "default" };
                opt.WorkerCount = hf.GetValue<int?>("WorkerCount") ?? 4;
            });

            services.AddScoped<HeartbeatJob>();
            services.AddScoped<LogCleanupJob>();
        });
        return builder;
    }

    /// <summary>
    /// 注册周期任务。host build 后调用。
    /// </summary>
    public static void UseSchedulerRecurringJobs(this IServiceProvider app)
    {
        var hf = app.GetRequiredService<IConfiguration>().GetSection("Hangfire");
        var q = hf["QueueName"] ?? "default";
        var tz = TimeZoneInfo.Utc;

        // 用 IRecurringJobManager (DI,非静态 RecurringJob) — 静态 API 在 JobStorage 注册前调会抛异常。
        var jobs = app.GetRequiredService<IRecurringJobManager>();

        // Hangfire 1.8: 带 queue 的 AddOrUpdate 重载均标 CS0618 (迁 2.0 提示),无非过时替代,临时抑制。
#pragma warning disable CS0618
        jobs.AddOrUpdate(
            "heartbeat",
            Job.FromExpression<HeartbeatJob>(x => x.RunAsync()),
            Cron.Minutely(), tz, q);

        jobs.AddOrUpdate(
            "log-cleanup",
            Job.FromExpression<LogCleanupJob>(x => x.RunAsync()),
            Cron.Daily(2), tz, q);
#pragma warning restore CS0618
    }
}
