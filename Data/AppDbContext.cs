using Microsoft.EntityFrameworkCore;
using MyFirstApi.Models;

namespace MyFirstApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<TrackedItem> TrackedItems { get; set; }
    public DbSet<ItemCategory> ItemCategories { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<Party> Parties { get; set; }
    public DbSet<EventType> EventTypes { get; set; }
    public DbSet<Carrier> Carriers { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Container> Containers { get; set; }
    public DbSet<TrackingEvent> TrackingEvents { get; set; }
    public DbSet<Shipment> Shipments { get; set; }
    public DbSet<ShipmentItem> ShipmentItems { get; set; }
    public DbSet<ShipmentLeg> ShipmentLegs { get; set; }
    public DbSet<ReasonCode> ReasonCodes { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();

        modelBuilder.Entity<Permission>()
            .HasIndex(p => p.Code)
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .HasIndex(rp => new { rp.Role, rp.PermissionId })
            .IsUnique();

        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.Permission)
            .WithMany()
            .HasForeignKey(rp => rp.PermissionId);

        modelBuilder.Entity<TrackedItem>(entity =>
        {
            entity.HasIndex(i => i.TagCode).IsUnique();
            entity.Property(i => i.Status).HasConversion<string>();
            entity.Property(i => i.Attributes).HasColumnType("jsonb");

            // Restrict: master data is deactivated, never deleted while referenced.
            entity.HasOne(i => i.Category).WithMany()
                .HasForeignKey(i => i.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.CurrentLocation).WithMany()
                .HasForeignKey(i => i.CurrentLocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.OwnerParty).WithMany()
                .HasForeignKey(i => i.OwnerPartyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.CurrentContainer).WithMany()
                .HasForeignKey(i => i.CurrentContainerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ItemCategory>().HasIndex(c => c.Code).IsUnique();

        modelBuilder.Entity<Location>(entity =>
        {
            entity.HasIndex(l => l.Code).IsUnique();
            entity.Property(l => l.Type).HasConversion<string>();
        });

        modelBuilder.Entity<Party>(entity =>
        {
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.Type).HasConversion<string>();
        });

        modelBuilder.Entity<EventType>(entity =>
        {
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.ResultingStatus).HasConversion<string>();
        });

        modelBuilder.Entity<Carrier>().HasIndex(c => c.Code).IsUnique();

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasIndex(v => v.Code).IsUnique();
            entity.Property(v => v.Mode).HasConversion<string>();
            entity.HasOne(v => v.Carrier).WithMany()
                .HasForeignKey(v => v.CarrierId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Container>(entity =>
        {
            entity.HasIndex(c => c.Code).IsUnique();
            entity.Property(c => c.Type).HasConversion<string>();
            entity.HasOne(c => c.ParentContainer).WithMany()
                .HasForeignKey(c => c.ParentContainerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(c => c.CurrentLocation).WithMany()
                .HasForeignKey(c => c.CurrentLocationId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TrackingEvent>(entity =>
        {
            entity.Property(e => e.Source).HasConversion<string>();
            entity.HasOne(e => e.TrackedItem).WithMany()
                .HasForeignKey(e => e.TrackedItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.EventType).WithMany()
                .HasForeignKey(e => e.EventTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Location).WithMany()
                .HasForeignKey(e => e.LocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Shipment).WithMany()
                .HasForeignKey(e => e.ShipmentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ShipmentLeg).WithMany()
                .HasForeignKey(e => e.ShipmentLegId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Container).WithMany()
                .HasForeignKey(e => e.ContainerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReasonCode).WithMany()
                .HasForeignKey(e => e.ReasonCodeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<TrackingEvent>().WithMany()
                .HasForeignKey(e => e.ReplacesEventId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReasonCode>().HasIndex(r => r.Code).IsUnique();

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasIndex(s => s.TrackingNumber).IsUnique();
            entity.Property(s => s.Status).HasConversion<string>();
            entity.Property(s => s.CustomsStatus).HasConversion<string>();
            entity.HasOne(s => s.SenderParty).WithMany()
                .HasForeignKey(s => s.SenderPartyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.ReceiverParty).WithMany()
                .HasForeignKey(s => s.ReceiverPartyId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.OriginLocation).WithMany()
                .HasForeignKey(s => s.OriginLocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.DestinationLocation).WithMany()
                .HasForeignKey(s => s.DestinationLocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(s => s.Legs).WithOne(l => l.Shipment)
                .HasForeignKey(l => l.ShipmentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.Items).WithOne(si => si.Shipment)
                .HasForeignKey(si => si.ShipmentId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShipmentItem>(entity =>
        {
            entity.HasKey(si => new { si.ShipmentId, si.TrackedItemId });
            entity.HasOne(si => si.TrackedItem).WithMany()
                .HasForeignKey(si => si.TrackedItemId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShipmentLeg>(entity =>
        {
            entity.HasIndex(l => new { l.ShipmentId, l.Sequence }).IsUnique();
            entity.Property(l => l.Mode).HasConversion<string>();
            entity.Property(l => l.DocumentType).HasConversion<string>();
            entity.HasOne(l => l.OriginLocation).WithMany()
                .HasForeignKey(l => l.OriginLocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(l => l.DestinationLocation).WithMany()
                .HasForeignKey(l => l.DestinationLocationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(l => l.Carrier).WithMany()
                .HasForeignKey(l => l.CarrierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(l => l.Vehicle).WithMany()
                .HasForeignKey(l => l.VehicleId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

