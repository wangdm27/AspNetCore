using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 用户-角色关联表（user_roles）的映射配置：复合主键、列名与级联删除关系
    /// </summary>
    internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
    {
        /// <summary>
        /// 配置用户-角色关联实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<UserRole> builder)
        {
            builder.ToTable("user_roles");
            builder.HasKey(x => new { x.TenantId, x.UserId, x.RoleId });

            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.RoleId).HasColumnName("role_id");
            builder.Property(x => x.AssignedAt).HasColumnName("assigned_at");

            builder.HasOne(x => x.User)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Role)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
