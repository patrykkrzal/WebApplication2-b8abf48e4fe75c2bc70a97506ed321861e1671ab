using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Rent.Models;

namespace Rent.Data
{
    public class DataContext : IdentityDbContext<User>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Worker> Workers { get; set; }
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<RentalInfo> RentalInfo { get; set; }
        public DbSet<OrderedItem> OrderedItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Deklaracja triggera na tabeli Orders (eliminuje konflikt OUTPUT + trigger)
            modelBuilder.Entity<Order>()
                .ToTable("Orders", t => t.HasTrigger("trg_Orders_ValidateDiscount"));

            modelBuilder.Entity<OrderedItem>()
                .HasKey(eo => new { eo.EquipmentId, eo.OrderId });

            modelBuilder.Entity<OrderedItem>()
                .HasOne(eo => eo.Equipment)
                .WithMany(e => e.OrderedItems)
                .HasForeignKey(eo => eo.EquipmentId);

            modelBuilder.Entity<OrderedItem>()
                .HasOne(eo => eo.Order)
                .WithMany(o => o.OrderedItems)
                .HasForeignKey(eo => eo.OrderId);
        }
    }
}




