using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AspNetCore.DataAccess.Entities
{
    [Table("menus")]
    public sealed class Menu
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [Column("parent_id")]
        public Guid? ParentId { get; set; }

        [MaxLength(50)]
        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        [Column("path")]
        public string Path { get; set; } = string.Empty;

        [MaxLength(200)]
        [Column("component")]
        public string Component { get; set; } = string.Empty;

        [MaxLength(100)]
        [Column("icon")]
        public string Icon { get; set; } = string.Empty;

        [Column("sort")]
        public int Sort { get; set; }

        [MaxLength(100)]
        [Column("permission_code")]
        public string PermissionCode { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public Menu? Parent { get; set; }
        public ICollection<Menu> Children { get; set; } = new List<Menu>();
    }
}
