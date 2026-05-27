using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AspNetCore.DataAccess.Entities
{
    [Table("users")]
    public sealed class User
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [MaxLength(50)]
        [Column("user_name")]
        public string UserName { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [MaxLength(512)]
        [Column("password_hash")]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(256)]
        [Column("password_salt")]
        public string PasswordSalt { get; set; } = string.Empty;

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }

        public ICollection<TenantUser> TenantUsers { get; set; } = new List<TenantUser>();
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    }
}
