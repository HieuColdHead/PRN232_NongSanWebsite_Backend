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
    public DbSet<EmailOtp> EmailOtps { get; set; }
    public DbSet<PendingRegistration> PendingRegistrations { get; set; }
    public DbSet<Provider> Providers { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<ProductImage> ProductImages { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderDetail> OrderDetails { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<Cart> Carts { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<UserVoucher> UserVouchers { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Blog> Blogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ?? User ??
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.DisplayName).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Ignore(e => e.Role);
            entity.Property(e => e.Provider).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ?? PendingRegistration ??
        modelBuilder.Entity<PendingRegistration>(entity =>
        {
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_PendingRegistrations_Email");

            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(20);
            entity.Property(e => e.DisplayName).HasMaxLength(150);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        });

        // ?? EmailOtp ??
        modelBuilder.Entity<EmailOtp>(entity =>
        {
            entity.HasIndex(e => new { e.Email, e.ExpiresAt });
            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.OtpHash).HasMaxLength(128);
        });

        // ?? Provider ??
        modelBuilder.Entity<Provider>(entity =>
        {
            entity.Property(e => e.RatingAverage).HasColumnType("decimal(3,2)");
        });

        // ?? Product ??
        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(e => e.BasePrice).HasColumnType("decimal(18,2)");
        });

        // ?? ProductVariant ??
        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
        });

        // ?? Order ??
        modelBuilder.Entity<Order>(entity =>
        {
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.ShippingFee).HasColumnType("decimal(18,2)");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18,2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(18,2)");
        });

        // ?? OrderDetail ??
        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
        });

        // ?? Review ??
        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.ProductId })
                .HasDatabaseName("IX_Reviews_UserId_ProductId");
        });

        // ?? Notification ??
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Notifications_UserId");
        });

        // ?? Cart ??
        modelBuilder.Entity<Cart>(entity =>
        {
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Carts_UserId");
        });

        // ?? CartItem ??
        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.Property(e => e.PriceAtTime).HasColumnType("decimal(18,2)");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18,2)");
        });

        // ?? Voucher ??
        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.Property(e => e.DiscountValue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MinOrderValue).HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaxDiscount).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("IX_Vouchers_Code");
        });

        // ?? UserVoucher ??
        modelBuilder.Entity<UserVoucher>(entity =>
        {
            entity.HasIndex(e => new { e.UserId, e.VoucherId })
                .HasDatabaseName("IX_UserVouchers_UserId_VoucherId");
        });

        // ?? Payment ??
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasIndex(e => e.OrderId)
                .HasDatabaseName("IX_Payments_OrderId");
        });

        // ?? Transaction ??
        modelBuilder.Entity<Transaction>(entity =>
        {
            entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => e.PaymentId)
                .HasDatabaseName("IX_Transactions_PaymentId");
        });

        // ?? Blog ??
        modelBuilder.Entity<Blog>(entity =>
        {
            entity.HasIndex(e => e.AuthorId)
                .HasDatabaseName("IX_Blogs_AuthorId");
        });
    }
}
