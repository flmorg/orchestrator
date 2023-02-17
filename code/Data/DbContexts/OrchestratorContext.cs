using Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Data.DbContexts;

public sealed class OrchestratorContext : DbContext
{
    public DbSet<Job> Jobs { get; set; }

    public DbSet<Trigger> Triggers { get; set; }

    public OrchestratorContext(DbContextOptions<OrchestratorContext> options) : base(options)
    {
    }
}