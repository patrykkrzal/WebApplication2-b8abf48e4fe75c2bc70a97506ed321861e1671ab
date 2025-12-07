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

        // Users DbSet is inherited from IdentityDbContext<User>, remove duplicate to avoid hiding warning
        // public DbSet<User> Users { get; set; }
        public DbSet<Worker> Workers { get; set; }
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<EquipmentPrice> EquipmentPrices { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<RentalInfo> RentalInfo { get; set; }
        public DbSet<OrderedItem> OrderedItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);


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

            // Set decimal precision to avoid truncation warnings
            modelBuilder.Entity<Equipment>().Property(e => e.Price).HasPrecision(18, 2);
            modelBuilder.Entity<EquipmentPrice>().Property(p => p.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Order>().Property(o => o.BasePrice).HasPrecision(18, 2);
            modelBuilder.Entity<OrderedItem>().Property(oi => oi.PriceWhenOrdered).HasPrecision(18, 2);

            // Map SQL scalar function dbo.fnOrderDiscount so it can be used in LINQ and translated to SQL
            modelBuilder
                .HasDbFunction(typeof(DataContext).GetMethod(nameof(FnOrderDiscount), new[] { typeof(int), typeof(int) }))
                .HasName("fnOrderDiscount")
                .HasSchema("dbo");
        }

        // CLR wrapper for the SQL function. EF will translate calls to this method into SQL calls to dbo.fnOrderDiscount.
        public static decimal FnOrderDiscount(int itemsCount, int days)
        {
            // This method is for use in LINQ queries and should not be executed client-side.
            throw new System.NotSupportedException("This method is only intended for use in LINQ-to-Entities queries and will be translated to SQL.");
        }
    }
}




