using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AspNetCore.DataAccess.Entities
{
    [Table("audit_logs")]
    public sealed class AuditLog
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("tenant_id")]
        public Guid? TenantId { get; set; }

        [Column("user_id")]
        public Guid? UserId { get; set; }

        [MaxLength(50)]
        [Column("user_name")]
        public string UserName { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("action")]
        public string Action { get; set; } = string.Empty;

        [MaxLength(50)]
        [Column("entity_type")]
        public string EntityType { get; set; } = string.Empty;

        [Column("entity_id")]
        public Guid? EntityId { get; set; }

        [MaxLength(2000)]
        [Column("details")]
        public string Details { get; set; } = string.Empty;

        [MaxLength(45)]
        [Column("ip_address")]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(500)]
        [Column("user_agent")]
        public string UserAgent { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
