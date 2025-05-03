using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PolarDrive.Data.DbContexts
{
    public class PolarDriveDbContextFactory : IDesignTimeDbContextFactory<PolarDriveDbContext>
    {
        public PolarDriveDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<PolarDriveDbContext>();

            // ⚠️ Usa sempre la stessa stringa usata da CLI e test
            optionsBuilder.UseSqlite("Data Source=datapolar.db");

            return new PolarDriveDbContext(optionsBuilder.Options);
        }
    }
}