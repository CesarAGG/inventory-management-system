using InventoryManagementSystem.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementSystem.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<Item> Items { get; set; }
    public DbSet<CustomField> CustomFields { get; set; }

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
    }
}