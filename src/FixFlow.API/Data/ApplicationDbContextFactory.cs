using FixFlow.API.Services.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FixFlow.API.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var apiProjectDir = Path.Combine(currentDir, "src", "FixFlow.API");
        var basePath = Directory.Exists(apiProjectDir) ? apiProjectDir : currentDir;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddUserSecrets(typeof(ApplicationDbContextFactory).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString) ||
            connectionString.Contains("YOUR_PASSWORD", StringComparison.OrdinalIgnoreCase))
        {
            connectionString = "Host=localhost;Port=5432;Database=fixflow_design;Username=postgres;Password=postgres";
        }

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
        {
            npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.DefaultSchema);
        });

        return new ApplicationDbContext(optionsBuilder.Options, new DesignTimeTenantService());
    }

    private sealed class DesignTimeTenantService : ICurrentTenantService
    {
        public Guid TenantId => Guid.Empty;
    }
}
