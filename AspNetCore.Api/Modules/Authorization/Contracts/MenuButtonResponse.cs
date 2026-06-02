namespace AspNetCore.Api.Modules.Authorization.Contracts
{
    public sealed class MenuButtonResponse
    {
        public Guid PermissionId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}
