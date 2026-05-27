namespace AspNetCore.Api.Infrastructure.Auth
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        public string Issuer { get; set; } = "AspNetCore.Api";

        public string Audience { get; set; } = "AspNetCore.Client";

        public string SigningKey { get; set; } = "AspNetCore-Replace-This-With-A-Strong-Key-1234567890";

        public int AccessTokenExpiresMinutes { get; set; } = 120;
    }
}
