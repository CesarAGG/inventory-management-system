using InventoryManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<CustomField> CustomFields { get; set; }
    public DbSet<InventoryUserPermission> InventoryUserPermissions { get; set; } // Add this

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // An Inventory has many Items, which are deleted when the inventory is deleted.
        builder.Entity<Inventory>()
            .HasMany(i => i.Items)
            .WithOne(t => t.Inventory)
            .HasForeignKey(t => t.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // An Inventory has many CustomFields, which are deleted when the inventory is deleted.
        builder.Entity<Inventory>()
            .HasMany(i => i.CustomFields)
            .WithOne(cf => cf.Inventory)
            .HasForeignKey(cf => cf.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // An Inventory has many Permissions, which are deleted when the inventory is deleted.
        builder.Entity<Inventory>()
            .HasMany(i => i.Permissions)
            .WithOne(p => p.Inventory)
            .HasForeignKey(p => p.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // A User can have many Permissions. When a user is deleted, their permissions are removed.
        builder.Entity<ApplicationUser>()
            .HasMany(u => u.Permissions)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Explicitly define the relationship between User (Owner) and Inventory.
        builder.Entity<Inventory>()
            .HasOne(i => i.Owner)
            .WithMany()
            .HasForeignKey(i => i.OwnerId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Configure concurrency token for Inventory
        builder.Entity<Inventory>()
            .Property(i => i.Version)
            .IsRowVersion();

        // Configure concurrency token for Item
        builder.Entity<Item>()
            .Property(i => i.Version)
            .IsRowVersion();

        builder.Entity<CustomField>()
            .HasIndex(cf => new { cf.InventoryId, cf.TargetColumn })
            .IsUnique();

        // This filtered index enforces that CustomId must be unique within an inventory
        builder.Entity<Item>()
            .HasIndex(i => new { i.InventoryId, i.CustomId })
            .IsUnique()
            .HasFilter(@"""CustomId"" <> ''");

        // Configure composite key for InventoryUserPermission
        builder.Entity<InventoryUserPermission>()
            .HasKey(p => new { p.InventoryId, p.UserId });
    }
}