using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 权限表（permissions）的映射配置：表名、列名、长度限制与唯一索引
    /// </summary>
    internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
    {
        /// <summary>
        /// 配置权限实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<Permission> builder)
        {
            builder.ToTable("permissions");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.Code).IsUnique();

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.Type).HasColumnName("type");
            builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
            builder.Property(x => x.HttpMethod).HasColumnName("http_method").HasMaxLength(20);
            builder.Property(x => x.Route).HasColumnName("route").HasMaxLength(200);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        }
    }
}
