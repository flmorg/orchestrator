using Data.Features.PriceGrabber.Models;
using Microsoft.EntityFrameworkCore;

namespace Data.DbContexts;

public sealed class OrchestratorContext : DbContext
{
    public DbSet<Store> Stores { get; set; }
    
    public DbSet<Product> Products { get; set; }

    public OrchestratorContext(DbContextOptions<OrchestratorContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Store>()
            .Property(x => x.Version)
            .IsRowVersion();
        modelBuilder.Entity<Product>()
            .Property(x => x.Version)
            .IsRowVersion();
    }
}
