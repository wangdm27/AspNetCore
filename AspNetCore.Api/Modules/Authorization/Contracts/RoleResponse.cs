namespace AspNetCore.Api.Modules.Authorization.Contracts
{
    public sealed class RoleResponse
    {
        public Guid RoleId { get; set; }

        public Guid TenantId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        public IReadOnlyCollection<string> Permissions { get; set; } = Array.Empty<string>();
    }
}
