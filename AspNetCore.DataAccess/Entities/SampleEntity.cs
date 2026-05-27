using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AspNetCore.DataAccess.Entities
{
    [Table("sample_entities")]
    public sealed class SampleEntity
    {
        [Key]
        [Column("id")]
        public Guid Id { get; set; }

        [MaxLength(100)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
