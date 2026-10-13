using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 租户表（tenants）的映射配置：表名、列名、长度限制与唯一索引
    /// </summary>
    internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
    {
        /// <summary>
        /// 配置租户实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            builder.ToTable("tenants");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.Code).IsUnique();

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        }
    }
}
