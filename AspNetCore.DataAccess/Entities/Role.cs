namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 角色实体；数据库映射见 Configurations/RoleConfiguration
    /// </summary>
    public sealed class Role
    {
        public Guid Id { get; set; }

        public Guid TenantId { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public Tenant? Tenant { get; set; }
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
