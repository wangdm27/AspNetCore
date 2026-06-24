namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class AuthResponse
    {
        public Guid UserId { get; set; }

        public Guid TenantId { get; set; }

        public string TenantCode { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

        public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
    }
}
