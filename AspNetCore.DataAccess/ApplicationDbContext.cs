using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.DataAccess
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Menu> Menus => Set<Menu>();
        public DbSet<Permission> Permissions => Set<Permission>();
        public DbSet<Role> Roles => Set<Role>();
        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
        public DbSet<SampleEntity> SampleEntities => Set<SampleEntity>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserRole> UserRoles => Set<UserRole>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            ConfigureSampleEntity(modelBuilder);
            ConfigureTenant(modelBuilder);
            ConfigureUser(modelBuilder);
            ConfigureTenantUser(modelBuilder);
            ConfigureRole(modelBuilder);
            ConfigurePermission(modelBuilder);
            ConfigureRolePermission(modelBuilder);
            ConfigureUserRole(modelBuilder);
            ConfigureMenu(modelBuilder);
        }

        private static void ConfigureSampleEntity(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SampleEntity>(entity =>
            {
                entity.ToTable("sample_entities");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            });
        }

        private static void ConfigureTenant(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tenant>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Code).IsUnique();
                entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            });
        }

        private static void ConfigureUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.UserName).IsUnique();
                entity.HasIndex(x => x.Email).IsUnique();
                entity.Property(x => x.UserName).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Email).HasMaxLength(100).IsRequired();
                entity.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
                entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
                entity.Property(x => x.PasswordSalt).HasMaxLength(256).IsRequired();
            });
        }

        private static void ConfigureTenantUser(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TenantUser>(entity =>
            {
                entity.HasKey(x => new { x.TenantId, x.UserId });
                entity.HasOne(x => x.Tenant)
                    .WithMany(x => x.TenantUsers)
                    .HasForeignKey(x => x.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.User)
                    .WithMany(x => x.TenantUsers)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureRole(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Role>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
                entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
                entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(500);
                entity.HasOne(x => x.Tenant)
                    .WithMany(x => x.Roles)
                    .HasForeignKey(x => x.TenantId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigurePermission(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Permission>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Code).IsUnique();
                entity.Property(x => x.Code).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Description).HasMaxLength(500);
                entity.Property(x => x.HttpMethod).HasMaxLength(20);
                entity.Property(x => x.Route).HasMaxLength(200);
            });
        }

        private static void ConfigureRolePermission(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RolePermission>(entity =>
            {
                entity.HasKey(x => new { x.RoleId, x.PermissionId });
                entity.HasOne(x => x.Role)
                    .WithMany(x => x.RolePermissions)
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Permission)
                    .WithMany(x => x.RolePermissions)
                    .HasForeignKey(x => x.PermissionId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureUserRole(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserRole>(entity =>
            {
                entity.HasKey(x => new { x.TenantId, x.UserId, x.RoleId });
                entity.HasOne(x => x.User)
                    .WithMany(x => x.UserRoles)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Role)
                    .WithMany(x => x.UserRoles)
                    .HasForeignKey(x => x.RoleId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }

        private static void ConfigureMenu(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Menu>(entity =>
            {
                entity.HasKey(x => x.Id);
                entity.HasIndex(x => x.Code).IsUnique();
                entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Path).HasMaxLength(200);
                entity.Property(x => x.Component).HasMaxLength(200);
                entity.Property(x => x.Icon).HasMaxLength(100);
                entity.Property(x => x.PermissionCode).HasMaxLength(100);
                entity.HasOne(x => x.Parent)
                    .WithMany(x => x.Children)
                    .HasForeignKey(x => x.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
