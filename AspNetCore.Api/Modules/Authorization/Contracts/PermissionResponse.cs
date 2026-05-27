namespace AspNetCore.Api.Modules.Authorization.Contracts
{
    public sealed class PermissionResponse
    {
        public Guid PermissionId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string HttpMethod { get; set; } = string.Empty;

        public string Route { get; set; } = string.Empty;
    }
}
