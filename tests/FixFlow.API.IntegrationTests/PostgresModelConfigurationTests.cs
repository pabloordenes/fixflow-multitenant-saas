using FixFlow.API.Data;
using FixFlow.API.Entities;
using FixFlow.API.Entities.WorkOrderModels;
using FixFlow.API.IntegrationTests.Infrastructure;
using FixFlow.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FixFlow.API.IntegrationTests;

public sealed class PostgresModelConfigurationTests
{
    private static ApplicationDbContext CreateNpgsqlModelOnlyContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=model_test;Username=postgres;Password=postgres",
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ApplicationDbContext.DefaultSchema))
            .Options;

        return new ApplicationDbContext(options, new TestTenantService { TenantId = Guid.Empty });
    }

    [Fact]
    public void DefaultSchema_IsFixflow_AndAllEntitiesUseFixflowSchema()
    {
        using var dbContext = CreateNpgsqlModelOnlyContext();

        Assert.Equal("fixflow", dbContext.Model.GetDefaultSchema());

        Type[] entityClrTypes =
        [
            typeof(Tenant),
            typeof(User),
            typeof(Customer),
            typeof(Product),
            typeof(WorkOrder),
            typeof(WorkOrderProduct),
            typeof(WorkOrderAttachment),
            typeof(WorkOrderHistory)
        ];

        foreach (var clrType in entityClrTypes)
        {
            var entityType = dbContext.Model.FindEntityType(clrType);
            Assert.NotNull(entityType);
            Assert.Equal("fixflow", entityType.GetSchema());
        }
    }

    [Fact]
    public void WorkOrderVersion_MapsToPostgreSqlXminSystemColumn()
    {
        using var dbContext = CreateNpgsqlModelOnlyContext();

        var workOrderType = dbContext.Model.FindEntityType(typeof(WorkOrder));
        Assert.NotNull(workOrderType);

        var versionProperty = workOrderType.FindProperty(nameof(WorkOrder.Version));
        Assert.NotNull(versionProperty);
        Assert.Equal("xmin", versionProperty.GetColumnName());
        Assert.Equal("xid", versionProperty.GetColumnType());
        Assert.True(versionProperty.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, versionProperty.ValueGenerated);
        Assert.Null(workOrderType.FindProperty("RowVersion"));
    }

    [Fact]
    public void MonetaryAndQuantityDecimals_HavePrecision18Scale2()
    {
        using var dbContext = CreateNpgsqlModelOnlyContext();

        var productPrice = dbContext.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(Product.Price))!;
        var woQuantity = dbContext.Model.FindEntityType(typeof(WorkOrderProduct))!.FindProperty(nameof(WorkOrderProduct.Quantity))!;
        var woUnitPrice = dbContext.Model.FindEntityType(typeof(WorkOrderProduct))!.FindProperty(nameof(WorkOrderProduct.UnitPriceAtTimeOfUse))!;

        Assert.Equal(18, productPrice.GetPrecision());
        Assert.Equal(2, productPrice.GetScale());
        Assert.Equal(18, woQuantity.GetPrecision());
        Assert.Equal(2, woQuantity.GetScale());
        Assert.Equal(18, woUnitPrice.GetPrecision());
        Assert.Equal(2, woUnitPrice.GetScale());
    }

    [Fact]
    public void Customer_HasUniqueCompositeIndexOnTenantIdAndRut()
    {
        using var dbContext = CreateNpgsqlModelOnlyContext();

        var customerType = dbContext.Model.FindEntityType(typeof(Customer))!;
        var tenantRutIndex = customerType.GetIndexes().FirstOrDefault(index =>
            index.Properties.Select(p => p.Name).SequenceEqual([nameof(Customer.TenantId), nameof(Customer.Rut)]));

        Assert.NotNull(tenantRutIndex);
        Assert.True(tenantRutIndex.IsUnique);
    }

    [Fact]
    public void AllTenantEntities_HaveGlobalQueryFilter()
    {
        using var dbContext = CreateNpgsqlModelOnlyContext();

        var tenantEntityTypes = dbContext.Model.GetEntityTypes()
            .Where(et => typeof(IMustHaveTenant).IsAssignableFrom(et.ClrType))
            .ToList();

        Assert.Equal(7, tenantEntityTypes.Count);

        foreach (var entityType in tenantEntityTypes)
        {
            Assert.NotEmpty(entityType.GetDeclaredQueryFilters());
        }
    }

    [Fact]
    public void PostgreSqlModel_HasNoPendingMigrationChanges()
    {
        using var dbContext = CreateNpgsqlModelOnlyContext();

        Assert.False(dbContext.Database.HasPendingModelChanges());
    }
}
