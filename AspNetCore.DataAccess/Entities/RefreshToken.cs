namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 刷新令牌实体；数据库映射见 Configurations/RefreshTokenConfiguration
    /// </summary>
    public sealed class RefreshToken
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public string TokenHash { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public bool IsUsed { get; set; }

        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }
    }
}
