namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 角色-权限关联实体；数据库映射见 Configurations/RolePermissionConfiguration
    /// </summary>
    public sealed class RolePermission
    {
        public Guid RoleId { get; set; }

        public Guid PermissionId { get; set; }

        public DateTime GrantedAt { get; set; }

        public Role? Role { get; set; }
        public Permission? Permission { get; set; }
    }
}
