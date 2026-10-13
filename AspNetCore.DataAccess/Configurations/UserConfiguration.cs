using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 用户表（users）的映射配置：表名、列名、长度限制与唯一索引
    /// </summary>
    internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
    {
        /// <summary>
        /// 配置用户实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("users");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.UserName).IsUnique();
            builder.HasIndex(x => x.Email).IsUnique();

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserName).HasColumnName("user_name").HasMaxLength(50).IsRequired();
            builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(100).IsRequired();
            builder.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(512).IsRequired();
            builder.Property(x => x.PasswordSalt).HasColumnName("password_salt").HasMaxLength(256).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        }
    }
}
