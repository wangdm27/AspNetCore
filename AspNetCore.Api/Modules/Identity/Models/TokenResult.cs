namespace AspNetCore.Api.Modules.Identity.Models
{
    public sealed class TokenResult
    {
        public string AccessToken { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }
    }
}
