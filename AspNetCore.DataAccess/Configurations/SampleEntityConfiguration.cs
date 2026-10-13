using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 示例实体表（sample_entities）的映射配置：表名、列名与长度限制
    /// </summary>
    internal sealed class SampleEntityConfiguration : IEntityTypeConfiguration<SampleEntity>
    {
        /// <summary>
        /// 配置示例实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<SampleEntity> builder)
        {
            builder.ToTable("sample_entities");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        }
    }
}
