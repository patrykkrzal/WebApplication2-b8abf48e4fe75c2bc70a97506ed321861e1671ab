using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Rent.Data
{
    public class DataContextFactory : IDesignTimeDbContextFactory<DataContext>
    {
        public DataContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DataContext>();


            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=RentDb;Trusted_Connection=True;");

            return new DataContext(optionsBuilder.Options);
        }
    }
}
