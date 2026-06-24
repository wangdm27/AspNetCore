namespace AspNetCore.Api.Infrastructure.Auth
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = "AspNetCore.Api";

        public string Audience { get; set; } = "AspNetCore.Client";

        /// <summary>
        /// JWT 签名密钥。生产环境应通过环境变量 JWT_SIGNING_KEY 设置，不应使用默认值。
        /// </summary>
        public string SigningKey { get; set; } = "AspNetCore-Replace-This-With-A-Strong-Key-1234567890";

        public int AccessTokenExpiresMinutes { get; set; } = 120;

        /// <summary>
        /// Refresh Token 有效天数
        /// </summary>
        public int RefreshTokenExpiresDays { get; set; } = 7;
    }
}
