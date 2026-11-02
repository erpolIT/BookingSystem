using BookingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace BookingSystem.Data;

public class BookingDbContext : IdentityDbContext<ApplicationUser>
{
    public BookingDbContext(DbContextOptions<BookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Booking> Bookings => Set<Booking>();

    public DbSet<Provider> Providers => Set<Provider>();

    public DbSet<WorkingHours> WorkingHours => Set<WorkingHours>();

    public DbSet<Service> Services => Set<Service>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Booking>(entity =>
        {
            entity.HasIndex(booking => new { booking.UserId, booking.IdempotencyKey }).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(booking => booking.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.Property(booking => booking.PriceAtBooking).HasPrecision(10, 2);
            entity.Property(booking => booking.RequestHash).HasMaxLength(64);
            entity.Property(booking => booking.CustomerName).HasMaxLength(100).IsRequired();
            entity.Property(booking => booking.CustomerEmail).HasMaxLength(254).IsRequired();
            entity.HasOne<Service>().WithMany().HasForeignKey(booking => booking.ServiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Provider>().WithMany().HasForeignKey(booking => booking.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("Bookings", table =>
            {
                table.HasCheckConstraint("CK_Bookings_Time", "\"StartTime\" < \"EndTime\"");
                table.HasCheckConstraint("CK_Bookings_Price", "\"PriceAtBooking\" IS NULL OR \"PriceAtBooking\" > 0");
                table.HasCheckConstraint("CK_Bookings_Status", "\"Status\" IN (0, 1)");
            });
        });

        modelBuilder.Entity<Provider>(entity =>
        {
            entity.Property(provider => provider.Name).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<WorkingHours>(entity =>
        {
            entity.HasIndex(hours => new { hours.ProviderId, hours.DayOfWeek }).IsUnique();
            entity.HasOne<Provider>().WithMany().HasForeignKey(hours => hours.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.ToTable("WorkingHours", table =>
            {
                table.HasCheckConstraint("CK_WorkingHours_Day", "\"DayOfWeek\" BETWEEN 0 AND 6");
                table.HasCheckConstraint("CK_WorkingHours_Time", "\"StartTime\" < \"EndTime\"");
            });
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.HasOne<Provider>()
                .WithMany()
                .HasForeignKey(service => service.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(service => service.Name)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(service => service.Price)
                .HasPrecision(10, 2);
        });
    }
}
