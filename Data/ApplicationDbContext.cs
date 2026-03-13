
using BE_TRELLO.Entities.Auth;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace BE_TRELLO.Data;


    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Users> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // Có thể cấu hình thêm các quan hệ giữa các bảng tại đây nếu cần

            builder.Entity<Users>(entity =>{
                entity.HasKey(u => u.UserId);

                entity.Property(u => u.UserId)
                    .HasDefaultValueSql("NEWID()");

                entity.Property(u => u.UserName).HasMaxLength(100).IsRequired(true);
                entity.HasIndex(u => u.UserName).IsUnique();


                
                entity.Property(u => u.Email).HasMaxLength(100).IsRequired(false);
                entity.HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");;

                entity.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired(false);
                entity.Property(u => u.GoogleId).HasMaxLength(100).IsRequired(false);
                entity.Property(u => u.IsActived).HasDefaultValue(true);
            });

          
        }
    }