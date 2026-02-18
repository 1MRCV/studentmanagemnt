using Microsoft.EntityFrameworkCore;
using StudentPortal.Web.Models; // Make sure this namespace points to your Student class

namespace StudentPortal.Web.DataContext
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Map Student entity
            modelBuilder.Entity<Student>(entity =>
            {
                entity.HasKey(e => e.Id); // Primary key

                entity.Property(e => e.Name)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(e => e.Email)
                      .HasMaxLength(100);

                entity.Property(e => e.Phone)
                      .HasMaxLength(20);

                entity.Property(e => e.Subscribed)
                      .IsRequired();
            });
        }
    }
}
