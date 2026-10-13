namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 用户-角色关联实体；数据库映射见 Configurations/UserRoleConfiguration
    /// </summary>
    public sealed class UserRole
    {
        public Guid TenantId { get; set; }

        public Guid UserId { get; set; }

        public Guid RoleId { get; set; }

        public DateTime AssignedAt { get; set; }

        public User? User { get; set; }
        public Role? Role { get; set; }
    }
}
