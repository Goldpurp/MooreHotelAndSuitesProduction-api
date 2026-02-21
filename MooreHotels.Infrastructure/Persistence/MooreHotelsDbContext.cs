using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MooreHotels.Domain.Entities;
using MooreHotels.Domain.Enums;
using System.Text.Json;

namespace MooreHotels.Infrastructure.Persistence;

public class MooreHotelsDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public MooreHotelsDbContext(DbContextOptions<MooreHotelsDbContext> options) : base(options) { }

    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomImage> RoomImages => Set<RoomImage>();
    public DbSet<Guest> Guests => Set<Guest>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<VisitRecord> VisitRecords => Set<VisitRecord>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Value Converter for List<string> to JSON
        var listConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
            v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!) ?? new List<string>()
        );

        // Value Comparer to resolve EF Core Change Tracking warnings for collections
        var listComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList()
        );

        builder.Entity<ApplicationUser>(entity => { entity.ToTable("users"); });
        builder.Entity<IdentityRole<Guid>>(entity => { entity.ToTable("roles"); });
        builder.Entity<IdentityUserRole<Guid>>(entity => { entity.ToTable("user_roles"); });

        builder.Entity<Room>(entity =>
        {
            entity.ToTable("rooms");
            entity.HasIndex(e => e.RoomNumber).IsUnique();
            entity.Property(e => e.Category).HasConversion<string>();
            entity.Property(e => e.Floor).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();

            entity.Property(e => e.Amenities)
                .HasColumnType("jsonb")
                .HasConversion(listConverter, listComparer);

            entity.HasMany(e => e.Images)
                .WithOne(i => i.Room)
                .HasForeignKey(i => i.RoomId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RoomImage>(entity =>
        {
            entity.ToTable("room_images");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Url).IsRequired();
            entity.Property(e => e.PublicId).IsRequired();
        });

        builder.Entity<Guest>(entity =>
        {
            entity.ToTable("guests");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        builder.Entity<Booking>(entity =>
        {
            entity.ToTable("bookings");
            entity.HasIndex(e => e.BookingCode).IsUnique();
            entity.Property(e => e.Status).HasConversion<string>();
            entity.Property(e => e.PaymentStatus).HasConversion<string>();
            entity.Property(e => e.PaymentMethod).HasConversion<string>();
            entity.Property(e => e.StatusHistoryJson).HasColumnType("jsonb");
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.Property(e => e.OldDataJson).HasColumnType("jsonb");
            entity.Property(e => e.NewDataJson).HasColumnType("jsonb");
        });

        builder.Entity<Notification>(entity =>
        {
            entity.ToTable("notifications");
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(e => e.Role).HasConversion<string>();
            entity.Property(e => e.Status).HasConversion<string>();
        });
    }
}