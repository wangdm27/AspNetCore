using AspNetCore.DataAccess.Entities;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore.DataAccess
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<SampleEntity> SampleEntities => Set<SampleEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SampleEntity>(entity =>
            {
                entity.ToTable("sample_entities");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.Name).HasColumnName("name");
                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
                entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            });
        }
    }
}
