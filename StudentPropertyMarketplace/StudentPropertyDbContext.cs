using Microsoft.EntityFrameworkCore;

namespace StudentPropertyMarketplace;

public class StudentPropertyDbContext : DbContext
{
    public DbSet<Property> Properties { get; set; }
    public DbSet<Application> Applications { get; set; }
}