namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 租户-用户关联实体；数据库映射见 Configurations/TenantUserConfiguration
    /// </summary>
    public sealed class TenantUser
    {
        public Guid TenantId { get; set; }

        public Guid UserId { get; set; }

        public bool IsTenantOwner { get; set; }

        public DateTime JoinedAt { get; set; }

        public Tenant? Tenant { get; set; }
        public User? User { get; set; }
    }
}
