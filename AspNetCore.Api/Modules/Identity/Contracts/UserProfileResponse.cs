namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class UserProfileResponse
    {
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public Guid TenantId { get; set; }

        public string TenantCode { get; set; } = string.Empty;

        public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();

        public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
    }
}
