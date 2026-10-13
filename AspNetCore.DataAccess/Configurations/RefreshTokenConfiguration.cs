using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AspNetCore.DataAccess.Configurations
{
    /// <summary>
    /// 刷新令牌表（refresh_tokens）的映射配置：表名、列名、唯一索引与用户外键
    /// </summary>
    internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        /// <summary>
        /// 配置刷新令牌实体与数据库表的映射
        /// </summary>
        /// <param name="builder">实体类型构建器</param>
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("refresh_tokens");
            builder.HasKey(x => x.Id);

            builder.HasIndex(x => x.TokenHash).IsUnique();

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(256).IsRequired();
            builder.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            builder.Property(x => x.IsUsed).HasColumnName("is_used");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
