using System.ComponentModel.DataAnnotations.Schema;

namespace AspNetCore.DataAccess.Entities
{
    [Table("role_permissions")]
    public sealed class RolePermission
    {
        [Column("role_id")]
        public Guid RoleId { get; set; }

        [Column("permission_id")]
        public Guid PermissionId { get; set; }

        [Column("granted_at")]
        public DateTime GrantedAt { get; set; }

        public Role? Role { get; set; }
        public Permission? Permission { get; set; }
    }
}
