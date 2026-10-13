using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 审计日志表（audit_logs）的映射配置：表名、列名、长度限制与查询索引
    /// </summary>
    internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
    {
        /// <summary>
        /// 配置审计日志实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<AuditLog> builder)
        {
            builder.ToTable("audit_logs");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.TenantId);
            builder.HasIndex(x => x.EntityType);
            builder.HasIndex(x => x.CreatedAt);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.UserName).HasColumnName("user_name").HasMaxLength(50).IsRequired();
            builder.Property(x => x.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
            builder.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(50).IsRequired();
            builder.Property(x => x.EntityId).HasColumnName("entity_id");
            builder.Property(x => x.Details).HasColumnName("details").HasMaxLength(2000);
            builder.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            builder.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        }
    }
}
