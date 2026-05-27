using System.ComponentModel.DataAnnotations.Schema;

namespace AspNetCore.DataAccess.Entities
{
    [Table("tenant_users")]
    public sealed class TenantUser
    {
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("is_tenant_owner")]
        public bool IsTenantOwner { get; set; }

        [Column("joined_at")]
        public DateTime JoinedAt { get; set; }

        public Tenant? Tenant { get; set; }
        public User? User { get; set; }
    }
}
