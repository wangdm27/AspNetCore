using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 租户-用户关联表（tenant_users）的映射配置：复合主键、列名与级联删除关系
    /// </summary>
    internal sealed class TenantUserConfiguration : IEntityTypeConfiguration<TenantUser>
    {
        /// <summary>
        /// 配置租户-用户关联实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<TenantUser> builder)
        {
            builder.ToTable("tenant_users");
            builder.HasKey(x => new { x.TenantId, x.UserId });

            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.IsTenantOwner).HasColumnName("is_tenant_owner");
            builder.Property(x => x.JoinedAt).HasColumnName("joined_at");

            builder.HasOne(x => x.Tenant)
                .WithMany(x => x.TenantUsers)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.User)
                .WithMany(x => x.TenantUsers)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
