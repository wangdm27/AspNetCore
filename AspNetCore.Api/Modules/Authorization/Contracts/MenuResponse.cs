namespace AspNetCore.Api.Modules.Authorization.Contracts
{
    public sealed class MenuResponse
    {
        public Guid MenuId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string Component { get; set; } = string.Empty;

        public string Icon { get; set; } = string.Empty;

        public int Sort { get; set; }

        public string PermissionCode { get; set; } = string.Empty;

        public IReadOnlyCollection<MenuResponse> Children { get; set; } = Array.Empty<MenuResponse>();
    }
}
