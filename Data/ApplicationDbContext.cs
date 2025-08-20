using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Web_chơi_cờ_Caro.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<BanRecord> BanRecords { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure BanRecord
            builder.Entity<BanRecord>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(500);
                entity.Property(e => e.BannedBy).IsRequired().HasMaxLength(256);
                entity.Property(e => e.UnbannedBy).HasMaxLength(256);
                
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
    public class ApplicationUser : IdentityUser
    {
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? LastLoginTime { get; set; }
    }

    public class BanRecord
    {
        public int Id { get; set; }
        public string UserId { get; set; } = "";
        public string Reason { get; set; } = "";
        public string BannedBy { get; set; } = "";
        public DateTime BannedAt { get; set; }
        public DateTime BanEndDate { get; set; }
        public bool IsActive { get; set; } = true;
        public string? UnbannedBy { get; set; }
        public DateTime? UnbannedAt { get; set; }

        // Navigation property
        public ApplicationUser User { get; set; } = null!;
    }

}
