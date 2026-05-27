namespace AspNetCore.Api.Modules.Tenancy.Contracts
{
    public sealed class TenantResponse
    {
        public Guid TenantId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
