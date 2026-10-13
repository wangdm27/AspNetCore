using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 菜单表（menus）的映射配置：表名、列名、唯一索引与父子自关联
    /// </summary>
    internal sealed class MenuConfiguration : IEntityTypeConfiguration<Menu>
    {
        /// <summary>
        /// 配置菜单实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<Menu> builder)
        {
            builder.ToTable("menus");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.Code).IsUnique();

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.ParentId).HasColumnName("parent_id");
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.Path).HasColumnName("path").HasMaxLength(200);
            builder.Property(x => x.Component).HasColumnName("component").HasMaxLength(200);
            builder.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(100);
            builder.Property(x => x.Sort).HasColumnName("sort");
            builder.Property(x => x.PermissionCode).HasColumnName("permission_code").HasMaxLength(100);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");

            builder.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
