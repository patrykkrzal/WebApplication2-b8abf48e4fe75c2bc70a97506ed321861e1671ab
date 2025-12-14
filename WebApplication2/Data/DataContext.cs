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

        public DbSet<Worker> Workers { get; set; }
        public DbSet<Equipment> Equipment { get; set; }
        public DbSet<EquipmentPrice> EquipmentPrices { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<RentalInfo> RentalInfo { get; set; }
        public DbSet<OrderedItem> OrderedItems { get; set; }
        public DbSet<OrderLog> OrderLogs { get; set; }

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


            modelBuilder.Entity<OrderLog>(eb =>
            {
                eb.ToTable("OrderLogs");
                eb.HasKey(l => l.Id);
                eb.Property(l => l.Message).HasMaxLength(4000).IsRequired();
                eb.Property(l => l.LogDate).HasDefaultValueSql("SYSUTCDATETIME()");
            });

            modelBuilder
                .HasDbFunction(typeof(DataContext).GetMethod(nameof(FnOrderDiscount), new[] { typeof(int), typeof(int) })!)
                .HasName("fnOrderDiscount")
                .HasSchema("dbo");
        }

        public static decimal FnOrderDiscount(int itemsCount, int days)
        {

            throw new System.NotSupportedException("This method is only intended for use in LINQ-to-Entities queries and will be translated to SQL.");
        }
    }
}







