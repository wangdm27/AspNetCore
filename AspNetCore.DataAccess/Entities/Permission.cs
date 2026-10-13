using AspNetCore.DataAccess.Entities.Enums;

namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 权限实体；数据库映射见 Configurations/PermissionConfiguration
    /// </summary>
    public sealed class Permission
    {
        public Guid Id { get; set; }

        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public PermissionType Type { get; set; }

        public string Description { get; set; } = string.Empty;

        public string HttpMethod { get; set; } = string.Empty;

        public string Route { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
