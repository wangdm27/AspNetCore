namespace AspNetCore.DataAccess.Entities
{
    /// <summary>
    /// 审计日志实体；数据库映射见 Configurations/AuditLogConfiguration
    /// </summary>
    public sealed class AuditLog
    {
        public Guid Id { get; set; }

        public Guid? TenantId { get; set; }

        public Guid? UserId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string EntityType { get; set; } = string.Empty;

        public Guid? EntityId { get; set; }

        public string Details { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;

        public string UserAgent { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }
    }
}
