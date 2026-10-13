using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.DataAccess
{
    /// <summary>
    /// 应用数据库上下文，聚合全部实体并装配各实体的 Fluent API 映射配置
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Menu> Menus => Set<Menu>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<SampleEntity> SampleEntities => Set<SampleEntity>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();

        /// <summary>
        /// 配置模型：自动装配本程序集内所有 IEntityTypeConfiguration 实现（Configurations 目录）
        /// </summary>
        /// <param name="modelBuilder">模型构建器</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}
