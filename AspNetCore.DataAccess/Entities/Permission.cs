using AspNetCore.DataAccess.Entities.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AspNetCore.DataAccess.Entities
{
    [Table("permissions")]
    public sealed class Permission
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [MaxLength(100)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("type")]
        public PermissionType Type { get; set; }

        [MaxLength(500)]
        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [MaxLength(20)]
        [Column("http_method")]
        public string HttpMethod { get; set; } = string.Empty;

        [MaxLength(200)]
        [Column("route")]
        public string Route { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    }
}
