
using BE_ECOMMERCE.Entities.Auth;

using Microsoft.EntityFrameworkCore;

namespace BE_ECOMMERCE.Data;


public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        // Có thể cấu hình thêm các quan hệ giữa các bảng tại đây nếu cần

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

           _ = entity.Property(u => u.RoleName).HasMaxLength(100).IsRequired(true);

       });

        _ = builder.Entity<Permission>(entity =>
       {
           _ = entity.HasKey(u => u.PermissionId);

           _ = entity.Property(u => u.PermissionId)
               .HasDefaultValueSql("NEWID()");

           _ = entity.Property(u => u.PermissionName).HasMaxLength(200).IsRequired(true);

       });

        _ = builder.Entity<RolePermission>(entity =>
       {
           _ = entity.HasKey(u => u.RolePermissionId);

           _ = entity.Property(u => u.RolePermissionId)
               .HasDefaultValueSql("NEWID()");

           _ = entity.Property(u => u.RoleId).IsRequired(true);
           _ = entity.Property(u => u.PermissionId).IsRequired(true);

       });




    }
}