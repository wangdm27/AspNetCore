namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 租户实体；数据库映射见 Configurations/TenantConfiguration
    /// </summary>
    public sealed class Tenant
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public ICollection<Role> Roles { get; set; } = new List<Role>();
        public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
    }
}
