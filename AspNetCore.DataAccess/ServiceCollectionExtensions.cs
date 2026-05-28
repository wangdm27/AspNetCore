using AspNetCore.DataAccess.Abstractions;
using AspNetCore.DataAccess.Dapper;
using AspNetCore.DataAccess.EntityFramework;
using AspNetCore.DataAccess.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetCore.DataAccess
{
    /// <summary>
    /// 服务集合扩展方法，用于统一配置和注册数据访问相关服务
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加统一数据访问服务到服务集合，根据配置自动选择 ORM 框架
        /// </summary>
        /// <typeparam name="TDbContext">Entity Framework Core 数据库上下文类型</typeparam>
        /// <param name="services">服务集合</param>
        /// <param name="configuration">应用程序配置</param>
        /// <returns>配置完成的服务集合</returns>
        public static IServiceCollection AddUnifiedDataAccess<TDbContext>(
            this IServiceCollection services,
            IConfiguration configuration)
            where TDbContext : DbContext
        {
            services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
            services.AddSingleton<IConnectionStringResolver, ConnectionStringResolver>();
            services.AddScoped<IDbConnectionFactory, DbConnectionFactory>();

            var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>()
                ?? new DatabaseOptions();
            var connectionString = new ConnectionStringResolver().ResolveConnectionString(databaseOptions, configuration);

            services.AddDbContext<TDbContext>(options =>
            {
                ConfigureDbContext(options, databaseOptions, connectionString);
            });

            if (databaseOptions.Orm == OrmType.EntityFrameworkCore)
            {
                RegisterEntityFramework<TDbContext>(services);
            }
            else
            {
                RegisterDapper(services);
            }

            return services;
        }

        /// <summary>
        /// 注册 Entity Framework Core 相关服务
        /// </summary>
        /// <typeparam name="TDbContext">Entity Framework Core 数据库上下文类型</typeparam>
        /// <param name="services">服务集合</param>
        /// <param name="databaseOptions">数据库配置选项</param>
        /// <param name="connectionString">数据库连接字符串</param>
        private static void RegisterEntityFramework<TDbContext>(IServiceCollection services)
            where TDbContext : DbContext
        {
            services.AddScoped<IRepositoryDbContext, EfRepositoryDbContext<TDbContext>>();
            services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
            services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        }

        /// <summary>
        /// 注册 Dapper 相关服务
        /// </summary>
        /// <param name="services">服务集合</param>
        private static void RegisterDapper(IServiceCollection services)
        {
            services.AddScoped<IDapperContext, DapperContext>();
            services.AddScoped(typeof(IRepository<>), typeof(DapperRepository<>));
            services.AddScoped<IUnitOfWork, DapperUnitOfWork>();
        }

        /// <summary>
        /// 配置 Entity Framework Core 数据库上下文选项
        /// </summary>
        /// <param name="optionsBuilder">数据库上下文选项构建器</param>
        /// <param name="databaseOptions">数据库配置选项</param>
        /// <param name="connectionString">数据库连接字符串</param>
        /// <exception cref="NotSupportedException">当指定的数据库提供程序不受支持时抛出</exception>
        private static void ConfigureDbContext(
            DbContextOptionsBuilder optionsBuilder,
            DatabaseOptions databaseOptions,
            string connectionString)
        {
            switch (databaseOptions.Provider)
            {
                case DatabaseProvider.SqlServer:
                    optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorNumbersToAdd: null);
                    });
                    break;
                case DatabaseProvider.PostgreSql:
                    optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                    {
                        npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorCodesToAdd: null);
                    });
                    break;
                default:
                    throw new NotSupportedException($"Database provider '{databaseOptions.Provider}' is not supported.");
            }
        }
    }
}
