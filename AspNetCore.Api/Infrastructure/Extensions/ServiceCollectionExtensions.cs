using AspNetCore.Api.Infrastructure.Auth;
using AspNetCore.Api.Infrastructure.Context;
using AspNetCore.Api.Infrastructure.Logging;
using AspNetCore.Api.Infrastructure.Services;
using AspNetCore.Api.Modules.Authorization.Services;
using AspNetCore.Api.Modules.Identity.Services;
using AspNetCore.Api.Modules.Tenancy.Services;
using AspNetCore.Logging;
using AspNetCore.RabbitMq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AspNetCore.Api.Infrastructure.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddBusinessModules(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddHttpContextAccessor();
            // 日志库用户上下文：HttpContextUserEnricher 在 host 启动后绑定此 provider，输出 UserId/TenantId
            services.AddSingleton<IUserContextProvider, HttpContextUserContextProvider>();
            services.AddScoped<ICurrentRequestContext, CurrentRequestContext>();
            services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
            services.AddScoped<ITokenService, JwtTokenService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ITenantService, TenantService>();
            services.AddScoped<IRoleService, RoleService>();
            services.AddScoped<IPermissionService, PermissionService>();
            services.AddScoped<IPermissionChecker, PermissionChecker>();
            services.AddScoped<IAuthorizationSeedService, AuthorizationSeedService>();
            services.AddScoped<IAuditLogService, AuditLogService>();
            services.AddScoped<IEmailService, SmtpEmailService>();
            services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

            // SigningKey：优先从环境变量 JWT_SIGNING_KEY 读取，其次从配置读取
            // 生产环境必须设置环境变量，否则启动时抛异常
            var signingKey = Environment.GetEnvironmentVariable("JWT_SIGNING_KEY") ?? jwtOptions.SigningKey;

            if (signingKey == "AspNetCore-Replace-This-With-A-Strong-Key-1234567890")
            {
                throw new InvalidOperationException(
                    "JWT SigningKey is using the default value. " +
                    "Set the 'Jwt:SigningKey' configuration or the 'JWT_SIGNING_KEY' environment variable with a strong key (at least 32 characters).");
            }

            jwtOptions.SigningKey = signingKey;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateIssuerSigningKey = true,
                        ValidateLifetime = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidAudience = jwtOptions.Audience,
                        IssuerSigningKey = key,
                        ClockSkew = TimeSpan.FromMinutes(1)
                    };
                });

            services.AddHostedService<DatabaseInitializationHostedService>();

            // RabbitMQ 基建 + 事件总线（发布端）。Api 仅发布，不注册任何消费者。
            var rmq = configuration.GetSection("RabbitMq");
            services.AddUnifiedRabbitMq(opt =>
            {
                opt.HostName = rmq["HostName"] ?? "localhost";
                opt.Port = rmq.GetValue<int?>("Port") ?? 5672;
                opt.UserName = rmq["UserName"] ?? "guest";
                opt.Password = rmq["Password"] ?? "guest";
                opt.VirtualHost = rmq["VirtualHost"] ?? "/";
                opt.ChannelPoolSize = rmq.GetValue<int?>("ChannelPoolSize") ?? 16;
            });
            services.AddRabbitMqEventBus(opt =>
            {
                opt.ExchangePrefix = configuration["EventBus:ExchangePrefix"] ?? "evt.";
                opt.QueuePrefix = configuration["EventBus:QueuePrefix"] ?? "q.";
                opt.ExchangeType = configuration["EventBus:ExchangeType"] ?? "direct";
            });

            return services;
        }
    }
}