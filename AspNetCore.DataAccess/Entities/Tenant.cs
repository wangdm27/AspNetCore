using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AspNetCore.DataAccess.Entities
{
    [Table("tenants")]
    public sealed class Tenant
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [MaxLength(50)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        public ICollection<Role> Roles { get; set; } = new List<Role>();
        public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
    }
}
