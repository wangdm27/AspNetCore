using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 角色-权限关联表（role_permissions）的映射配置：复合主键、列名与级联删除关系
    /// </summary>
    internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
    {
        /// <summary>
        /// 配置角色-权限关联实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<RolePermission> builder)
        {
            builder.ToTable("role_permissions");
            builder.HasKey(x => new { x.RoleId, x.PermissionId });

            builder.Property(x => x.RoleId).HasColumnName("role_id");
            builder.Property(x => x.PermissionId).HasColumnName("permission_id");
            builder.Property(x => x.GrantedAt).HasColumnName("granted_at");

            builder.HasOne(x => x.Role)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Permission)
                .WithMany(x => x.RolePermissions)
                .HasForeignKey(x => x.PermissionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
