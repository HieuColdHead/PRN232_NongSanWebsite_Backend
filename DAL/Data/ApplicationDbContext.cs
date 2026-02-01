using DAL.Entity;
using Microsoft.EntityFrameworkCore;

namespace DAL.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            entity.Property(e => e.Email)
                .HasMaxLength(255);

            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20);

            entity.Property(e => e.DisplayName)
                .HasMaxLength(150);

            entity.Property(e => e.Provider)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Id)
                .ValueGeneratedOnAdd();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Provider>(entity =>
        {
            entity.Property(e => e.RatingAverage).HasColumnType("decimal(3,2)");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.BasePrice).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
        });
    }
}
