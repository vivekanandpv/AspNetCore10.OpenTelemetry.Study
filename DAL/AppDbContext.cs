using AspNetCore10.OpenTelemetry.Study.Models;
using Microsoft.EntityFrameworkCore;

namespace AspNetCore10.OpenTelemetry.Study.DAL;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Customer>().HasData(
            new Customer { Id = 1, Name = "Alice Johnson", Email = "alice.johnson@example.com", PhoneNumber = "555-0101" },
            new Customer { Id = 2, Name = "Bob Smith", Email = "bob.smith@example.com", PhoneNumber = "555-0102" },
            new Customer { Id = 3, Name = "Charlie Brown", Email = "charlie.brown@example.com", PhoneNumber = "555-0103" },
            new Customer { Id = 4, Name = "Diana Prince", Email = "diana.prince@example.com", PhoneNumber = "555-0104" },
            new Customer { Id = 5, Name = "Ethan Hunt", Email = "ethan.hunt@example.com", PhoneNumber = "555-0105" },
            new Customer { Id = 6, Name = "Fiona Gallagher", Email = "fiona.gallagher@example.com", PhoneNumber = "555-0106" },
            new Customer { Id = 7, Name = "George Costanza", Email = "george.costanza@example.com", PhoneNumber = "555-0107" },
            new Customer { Id = 8, Name = "Hannah Abbott", Email = "hannah.abbott@example.com", PhoneNumber = "555-0108" },
            new Customer { Id = 9, Name = "Ian Malcolm", Email = "ian.malcolm@example.com", PhoneNumber = "555-0109" },
            new Customer { Id = 10, Name = "Jane Doe", Email = "jane.doe@example.com", PhoneNumber = "555-0110" }
        );
    }
}
