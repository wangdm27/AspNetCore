using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 角色表（roles）的映射配置：表名、列名、组合唯一索引与租户外键
    /// </summary>
    internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
    {
        /// <summary>
        /// 配置角色实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<Role> builder)
        {
            builder.ToTable("roles");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
            builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            builder.Property(x => x.IsDefault).HasColumnName("is_default");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            builder.HasOne(x => x.Tenant)
                .WithMany(x => x.Roles)
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
