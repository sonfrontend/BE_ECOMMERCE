using BE_ECOMMERCE.Entities.Auth;
using BE_ECOMMERCE.Entities.Product;
using BE_ECOMMERCE.Entities.Transaction;
using BE_ECOMMERCE.Entities.Category;
using BE_ECOMMERCE.Entities.Cart;
using BE_ECOMMERCE.Entities.Order;
using BE_ECOMMERCE.Entities.Promotion;
using BE_ECOMMERCE.Entities.System;
using BE_ECOMMERCE.Entities.Chat;
using BE_ECOMMERCE.Entities;
using BE_ECOMMERCE.Constants;

using BE_ECOMMERCE.Entities;
using BE_ECOMMERCE.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BE_ECOMMERCE.Data;


public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Complaint> Complaints { get; set; }
    public DbSet<ComplaintReason> ComplaintReasons { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<UserAddress> UserAddresses { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductVariant> ProductVariants { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<CartItem> CartItems { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }
    public DbSet<Promotion> Promotions { get; set; }
    public DbSet<Voucher> Vouchers { get; set; }
    public DbSet<UserVoucher> UserVouchers { get; set; }
    public DbSet<UserPromotion> UserPromotions { get; set; }
    public DbSet<Favorite> Favorites { get; set; }
    public DbSet<UserInteraction> UserInteractions { get; set; }
    public DbSet<ShippingFee> ShippingFees { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<SandboxWallet> SandboxWallets { get; set; }
    public DbSet<TransactionHistory> TransactionHistories { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ResolutionTemplate> ResolutionTemplates { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Có thể cấu hình thêm các quan hệ giữa các bảng tại đây nếu cần

        builder.Entity<ComplaintReason>().HasData(
            new ComplaintReason { Id = 1, Title = "Tôi chưa nhận được hàng", IsActive = true, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 1) },
            new ComplaintReason { Id = 2, Title = "Sản phẩm lỗi/hư hỏng nặng không thể sử dụng", IsActive = true, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 1) },
            new ComplaintReason { Id = 3, Title = "Sản phẩm bị lỗi nhẹ / Thiếu linh kiện, phụ kiện", IsActive = true, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 1) },
            new ComplaintReason { Id = 4, Title = "Giao sai sản phẩm / Sai màu / Sai kích cỡ", IsActive = true, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 1) },
            new ComplaintReason { Id = 5, Title = "Hàng không giống mô tả / Nghi ngờ hàng giả / Lý do khác", IsActive = true, CreatedAt = new DateTime(2024, 1, 1), UpdatedAt = new DateTime(2024, 1, 1) }
        );



        builder.Entity<SandboxWallet>().HasData(
            new SandboxWallet
            {
                Id = 1,
                AccountType = "ADMIN",
                AccountName = "PayPal Business (Admin)",
                Balance = 0
            }
        );

        _ = builder.Entity<User>(entity =>
        {
            _ = entity.HasKey(u => u.UserId);

            _ = entity.Property(u => u.UserId)
                .HasDefaultValueSql("NEWID()");

            _ = entity.Property(u => u.UserName).HasMaxLength(100).IsRequired(true);
            _ = entity.HasIndex(u => u.UserName).IsUnique();



            _ = entity.Property(u => u.Email).HasMaxLength(100).IsRequired(false);
            _ = entity.HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");

            _ = entity.Property(u => u.FullName).HasMaxLength(500).IsRequired(false);
            _ = entity.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired(false);
            _ = entity.Property(u => u.PhoneNumber).HasMaxLength(15).IsRequired(false);
            _ = entity.Property(u => u.RefreshToken).HasMaxLength(500).IsRequired(false);
            _ = entity.Property(u => u.RefreshTokenExpiryTime).IsRequired(false);
            _ = entity.Property(u => u.AvatarUrl).HasMaxLength(500).IsRequired(false);
            _ = entity.Property(u => u.GoogleId).HasMaxLength(100).IsRequired(false);
            _ = entity.Property(u => u.IsActived).HasDefaultValue(true);
        });

        _ = builder.Entity<Role>(entity =>
       {
           _ = entity.HasKey(u => u.RoleId);

           _ = entity.Property(u => u.RoleId)
               .HasDefaultValueSql("NEWID()");

           _ = entity.Property(u => u.RoleName).HasMaxLength(200).IsRequired(true);

       });

        _ = builder.Entity<Permission>(entity =>
       {
           _ = entity.HasKey(u => u.PermissionId);

           _ = entity.Property(u => u.PermissionId)
               .HasDefaultValueSql("NEWID()");

           _ = entity.Property(u => u.PermissionName).HasMaxLength(200).IsRequired(true);
           _ = entity.Property(u => u.Description).HasMaxLength(500).IsRequired(true);

       });

        _ = builder.Entity<RolePermission>(entity =>
       {
           _ = entity.HasKey(u => u.RolePermissionId);

           _ = entity.Property(u => u.RolePermissionId)
               .HasDefaultValueSql("NEWID()");

           _ = entity.Property(e => e.RoleId).IsRequired(true);
           _ = entity.Property(e => e.PermissionId).IsRequired(true);

           entity.HasOne(rp => rp.Role)
          .WithMany(r => r.RolePermissions)
          .HasForeignKey(rp => rp.RoleId);

           entity.HasOne(rp => rp.Permission)
          .WithMany(r => r.RolePermissions)
          .HasForeignKey(rp => rp.PermissionId);
           // Chống trùng key
           entity.HasIndex(rp => new { rp.RoleId, rp.PermissionId })
         .IsUnique();

       });

        _ = builder.Entity<UserRole>(entity =>
       {
           _ = entity.HasKey(u => u.UserRoleId);

           _ = entity.Property(u => u.UserRoleId)
               .HasDefaultValueSql("NEWID()");

           _ = entity.Property(e => e.UserId).IsRequired(true);
           _ = entity.Property(e => e.RoleId).IsRequired(true);

           entity.HasOne(ur => ur.User)
          .WithMany(u => u.UserRoles)
          .HasForeignKey(ur => ur.UserId);

           entity.HasOne(ur => ur.Role)
          .WithMany(r => r.UserRoles)
          .HasForeignKey(ur => ur.RoleId);

           // Chống trùng key
           entity.HasIndex(ur => new { ur.UserId, ur.RoleId })
         .IsUnique();
       });

        _ = builder.Entity<Category>(entity =>
        {
            _ = entity.HasKey(c => c.Id);
            _ = entity.Property(c => c.IconUrl).HasMaxLength(500).IsRequired(false);

            _ = entity.HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        _ = builder.Entity<Promotion>(entity =>
        {
            _ = entity.HasKey(p => p.Id);
            _ = entity.Property(p => p.Title).HasMaxLength(255).IsRequired(true);
            _ = entity.Property(p => p.ImageUrl).HasMaxLength(500).IsRequired(true);
            _ = entity.Property(p => p.DiscountPercentage).HasColumnType("decimal(5,2)").HasDefaultValue(0);
        });

        _ = builder.Entity<Voucher>(entity =>
        {
            _ = entity.HasKey(v => v.Id);
            _ = entity.Property(v => v.Code).HasMaxLength(50).IsRequired(true);
            _ = entity.HasIndex(v => v.Code).IsUnique();
            _ = entity.Property(v => v.DiscountValue).HasColumnType("decimal(18,2)");
            _ = entity.Property(v => v.MinOrderValue).HasColumnType("decimal(18,2)");
        });

        _ = builder.Entity<UserVoucher>(entity =>
        {
            _ = entity.HasKey(uv => uv.Id);
            _ = entity.HasOne(uv => uv.User)
                      .WithMany()
                      .HasForeignKey(uv => uv.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            _ = entity.HasOne(uv => uv.Voucher)
                      .WithMany()
                      .HasForeignKey(uv => uv.VoucherId)
                      .OnDelete(DeleteBehavior.Cascade);
            _ = entity.HasIndex(uv => new { uv.UserId, uv.VoucherId }).IsUnique();
        });

        _ = builder.Entity<Product>(entity =>
        {
            _ = entity.HasKey(p => p.ProductId);

            _ = entity.Property(p => p.ProductId).IsRequired(true);
            _ = entity.Property(p => p.ProductName).HasMaxLength(255).IsRequired(false);
            _ = entity.Property(p => p.ImageUrl).HasMaxLength(500).IsRequired(false);
            _ = entity.Property(p => p.Description).HasMaxLength(4000).IsRequired(false);

            // Configure relationship with Category
            _ = entity.HasOne(p => p.Categories)
                .WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        _ = builder.Entity<ProductVariant>(entity =>
        {
            _ = entity.HasKey(p => p.VariantId);
            _ = entity.Property(p => p.SKU).HasMaxLength(255).IsRequired(false);
            _ = entity.Property(p => p.Color).HasMaxLength(255).IsRequired(false);
            _ = entity.Property(p => p.Size).HasMaxLength(255).IsRequired(false);
            _ = entity.Property(p => p.OriginalPrice).HasColumnType("decimal(18,2)");
            _ = entity.Property(p => p.CurrentPrice).HasColumnType("decimal(18,2)");
            _ = entity.Property(p => p.ImageUrl).HasMaxLength(500).IsRequired(false);

            _ = entity.HasOne(pv => pv.Product)
                .WithMany(p => p.ProductVariants)
                .HasForeignKey(pv => pv.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        _ = builder.Entity<Transaction>(entity =>
        {
            _ = entity.HasKey(t => t.Id);

            _ = entity.HasOne(t => t.ProductVariant)
                .WithMany()
                .HasForeignKey(t => t.VariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        _ = builder.Entity<CartItem>(entity =>
        {
            _ = entity.HasKey(c => c.Id);

            _ = entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = entity.HasOne(c => c.ProductVariant)
                .WithMany()
                .HasForeignKey(c => c.VariantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        _ = builder.Entity<Order>(entity =>
        {
            _ = entity.HasKey(o => o.Id);
            _ = entity.HasOne(o => o.User)
                .WithMany()
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            _ = entity.Property(o => o.Status)
                .HasConversion<string>();
        });

        _ = builder.Entity<Complaint>(entity =>
        {
            _ = entity.HasKey(c => c.Id);
            _ = entity.HasOne(c => c.Order)
                .WithMany(o => o.Complaints)
                .HasForeignKey(c => c.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            
            _ = entity.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            _ = entity.HasOne(c => c.ResolutionTemplate)
                .WithMany()
                .HasForeignKey(c => c.HandlingMethodId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        _ = builder.Entity<OrderItem>(entity =>
        {
            _ = entity.HasKey(o => o.Id);
            _ = entity.HasOne(o => o.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(o => o.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            
            _ = entity.HasOne(o => o.ProductVariant)
                .WithMany()
                .HasForeignKey(o => o.VariantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        _ = builder.Entity<Favorite>(entity =>
        {
            _ = entity.HasKey(f => f.Id);

            _ = entity.HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = entity.HasOne(f => f.Product)
                .WithMany()
                .HasForeignKey(f => f.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Một User chỉ có thể favorite 1 Product 1 lần
            _ = entity.HasIndex(f => new { f.UserId, f.ProductId }).IsUnique();
        });

        _ = builder.Entity<Review>(entity =>
        {
            _ = entity.HasKey(r => r.Id);

            _ = entity.HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            _ = entity.HasOne(r => r.Product)
                .WithMany()
                .HasForeignKey(r => r.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            _ = entity.HasOne(r => r.OrderItem)
                .WithMany()
                .HasForeignKey(r => r.OrderItemId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Một OrderItem chỉ được review 1 lần
            _ = entity.HasIndex(r => r.OrderItemId).IsUnique();
        });

        _ = builder.Entity<ChatMessage>(entity =>
        {
            _ = entity.HasKey(cm => cm.Id);
            
            _ = entity.HasOne(cm => cm.User)
                .WithMany()
                .HasForeignKey(cm => cm.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        _ = builder.Entity<ResolutionTemplate>(entity =>
        {
            _ = entity.HasKey(rt => rt.Id);
            
            _ = entity.HasData(
                new ResolutionTemplate { Id = 1, Code = "FULL_REFUND", Title = "Hoàn tiền toàn bộ (Sản phẩm lỗi/hỏng nặng)", Description = "Hủy đơn hàng và hoàn lại 100% số tiền cho khách hàng. Yêu cầu khách hàng gửi trả lại hàng hoặc Admin tự thu hồi.", RestoresInventory = false, IsFullRefund = true, RequiresRefund = true, FinalOrderStatus = OrderStatus.Cancelled.ToString() },
                new ResolutionTemplate { Id = 2, Code = "PARTIAL_REFUND", Title = "Hoàn tiền một phần (Sản phẩm lỗi nhẹ/Thiếu phụ kiện)", Description = "Thỏa thuận hoàn lại một phần tiền qua chuyển khoản thủ công. Đơn hàng chuyển sang trạng thái Hoàn thành.", RestoresInventory = false, IsFullRefund = false, RequiresRefund = true, FinalOrderStatus = OrderStatus.Completed.ToString() },
                new ResolutionTemplate { Id = 3, Code = "EXCHANGE", Title = "Đổi trả sản phẩm (Giao sai màu/size)", Description = "Tạo đơn giao lại hàng đúng cho khách. Sau khi khách nhận được, đóng khiếu nại và chuyển trạng thái Hoàn thành.", RestoresInventory = true, IsFullRefund = true, RequiresRefund = true, FinalOrderStatus = OrderStatus.Completed.ToString() },
                new ResolutionTemplate { Id = 4, Code = "NOT_RECEIVED", Title = "Khách chưa nhận được hàng (Thất lạc do vận chuyển)", Description = "Xác nhận với đơn vị vận chuyển. Hủy đơn hàng trên hệ thống và hoàn lại 100% tiền.", RestoresInventory = false, IsFullRefund = true, RequiresRefund = false, FinalOrderStatus = OrderStatus.Cancelled.ToString() },
                new ResolutionTemplate { Id = 5, Code = "REJECTED", Title = "Từ chối khiếu nại (Không đủ bằng chứng)", Description = "Đóng khiếu nại, không hoàn tiền. Giải thích rõ ràng cho khách hàng và chuyển trạng thái đơn hàng thành Hoàn thành.", RestoresInventory = false, IsFullRefund = false, RequiresRefund = false, FinalOrderStatus = OrderStatus.Completed.ToString() }
            );
        });
    }
}