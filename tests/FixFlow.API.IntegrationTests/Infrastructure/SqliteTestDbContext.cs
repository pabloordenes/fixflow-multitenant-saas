using FixFlow.API.Data;
using FixFlow.API.Entities.WorkOrderModels;
using FixFlow.API.Services.Tenant;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests.Infrastructure;

public sealed class SqliteTestDbContext : ApplicationDbContext
{
    public SqliteTestDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentTenantService currentTenantService) : base(options, currentTenantService)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // SQLite does not auto-generate SQL Server rowversion byte[] values on INSERT.
        modelBuilder.Entity<WorkOrder>()
            .Property(w => w.RowVersion)
            .HasDefaultValue(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
    }
}
