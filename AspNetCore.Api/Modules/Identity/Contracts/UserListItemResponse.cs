namespace AspNetCore.Api.Modules.Identity.Contracts
{
    public sealed class UserListItemResponse
    {
        public Guid UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public bool IsTenantOwner { get; set; }

        public bool IsActive { get; set; }

        public IReadOnlyCollection<string> Roles { get; set; } = Array.Empty<string>();
    }
}
