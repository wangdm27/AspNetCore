using System.ComponentModel.DataAnnotations.Schema;

namespace AspNetCore.DataAccess.Entities
{
    [Table("user_roles")]
    public sealed class UserRole
    {
        [Column("tenant_id")]
        public Guid TenantId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("role_id")]
        public Guid RoleId { get; set; }

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; }

        public User? User { get; set; }
        public Role? Role { get; set; }
    }
}
