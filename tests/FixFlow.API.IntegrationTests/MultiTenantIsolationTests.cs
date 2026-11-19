using FixFlow.API.Data;
using FixFlow.API.Entities;
using FixFlow.API.Entities.WorkOrderModels;
using FixFlow.API.IntegrationTests.Infrastructure;
using FixFlow.Shared.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FixFlow.API.IntegrationTests;

public sealed class MultiTenantIsolationTests
{
    private static DbContextOptions<ApplicationDbContext> CreateOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    private static async Task<(Guid TenantAId, Guid TenantBId, Guid ProductBId, Guid CustomerAId, Guid CustomerBId)> SeedTwoTenantsAsync(
        SqliteConnection connection)
    {
        var options = CreateOptions(connection);
        var seedTenantService = new TestTenantService { TenantId = Guid.Empty };

        await using var seedContext = new SqliteTestDbContext(options, seedTenantService);
        await seedContext.Database.EnsureCreatedAsync();

        var tenantA = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Tenant Alpha",
            TaxId = "76.111.111-1",
            Industry = "HVAC",
            IsActive = true
        };
        var tenantB = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Tenant Beta",
            TaxId = "76.222.222-2",
            Industry = "Electrical",
            IsActive = true
        };

        var userA = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Tech Alpha",
            Email = "tech@alpha.cl",
            PasswordHash = "hashA",
            Role = "Technician",
            TenantId = tenantA.Id
        };
        var userB = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Tech Beta",
            Email = "tech@beta.cl",
            PasswordHash = "hashB",
            Role = "Technician",
            TenantId = tenantB.Id
        };

        var customerA = new Customer
        {
            Id = Guid.NewGuid(),
            Rut = "77.111.111-1",
            BusinessName = "Client Alpha",
            MainAddress = "Av. Alpha 100",
            TenantId = tenantA.Id
        };
        var customerB = new Customer
        {
            Id = Guid.NewGuid(),
            Rut = "77.222.222-2",
            BusinessName = "Client Beta",
            MainAddress = "Av. Beta 200",
            TenantId = tenantB.Id
        };

        var productA = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Filter Alpha",
            Price = 1000m,
            StockQuantity = 10,
            TenantId = tenantA.Id
        };
        var productB = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Breaker Beta",
            Price = 2000m,
            StockQuantity = 20,
            TenantId = tenantB.Id
        };
        var standaloneProductB = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Standalone Beta",
            Price = 3000m,
            StockQuantity = 5,
            TenantId = tenantB.Id
        };

        var workOrderA = new WorkOrder
        {
            Id = Guid.NewGuid(),
            Title = "WO Alpha",
            Description = "Alpha service",
            Status = WorkOrderStatus.Pending,
            ScheduledDate = DateTime.UtcNow.AddDays(1),
            Address = "Av. Alpha 100",
            TenantId = tenantA.Id,
            CustomerId = customerA.Id,
            AssignedTechnicianId = userA.Id
        };
        var workOrderB = new WorkOrder
        {
            Id = Guid.NewGuid(),
            Title = "WO Beta",
            Description = "Beta service",
            Status = WorkOrderStatus.InProgress,
            ScheduledDate = DateTime.UtcNow.AddDays(2),
            Address = "Av. Beta 200",
            TenantId = tenantB.Id,
            CustomerId = customerB.Id,
            AssignedTechnicianId = userB.Id
        };

        var woProductA = new WorkOrderProduct
        {
            Id = Guid.NewGuid(),
            WorkOrderId = workOrderA.Id,
            ProductId = productA.Id,
            Quantity = 1m,
            UnitPriceAtTimeOfUse = 1000m,
            TenantId = tenantA.Id
        };
        var woProductB = new WorkOrderProduct
        {
            Id = Guid.NewGuid(),
            WorkOrderId = workOrderB.Id,
            ProductId = productB.Id,
            Quantity = 2m,
            UnitPriceAtTimeOfUse = 2000m,
            TenantId = tenantB.Id
        };

        var attachmentA = new WorkOrderAttachment
        {
            Id = Guid.NewGuid(),
            FileUri = "https://storage.test/alpha.jpg",
            Type = AttachmentType.PhotoBefore,
            WorkOrderId = workOrderA.Id,
            TenantId = tenantA.Id
        };
        var attachmentB = new WorkOrderAttachment
        {
            Id = Guid.NewGuid(),
            FileUri = "https://storage.test/beta.jpg",
            Type = AttachmentType.PhotoAfter,
            WorkOrderId = workOrderB.Id,
            TenantId = tenantB.Id
        };

        var historyA = new WorkOrderHistory
        {
            Id = Guid.NewGuid(),
            PreviousStatus = "Pending",
            NewStatus = "OnGoing",
            WorkOrderId = workOrderA.Id,
            ChangedByUserId = userA.Id,
            TenantId = tenantA.Id
        };
        var historyB = new WorkOrderHistory
        {
            Id = Guid.NewGuid(),
            PreviousStatus = "OnGoing",
            NewStatus = "InProgress",
            WorkOrderId = workOrderB.Id,
            ChangedByUserId = userB.Id,
            TenantId = tenantB.Id
        };

        seedContext.Tenants.AddRange(tenantA, tenantB);
        seedContext.Users.AddRange(userA, userB);
        seedContext.Customers.AddRange(customerA, customerB);
        seedContext.Products.AddRange(productA, productB, standaloneProductB);
        seedContext.WorkOrders.AddRange(workOrderA, workOrderB);
        seedContext.WorkOrderProducts.AddRange(woProductA, woProductB);
        seedContext.WorkOrderAttachments.AddRange(attachmentA, attachmentB);
        seedContext.WorkOrderHistories.AddRange(historyA, historyB);
        await seedContext.SaveChangesAsync();

        return (tenantA.Id, tenantB.Id, standaloneProductB.Id, customerA.Id, customerB.Id);
    }

    [Fact]
    public async Task QueryFilters_IsolateAllSevenTenantEntities_AcrossDistinctDbContextInstancesWithCachedModel()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var (tenantAId, tenantBId, _, _, _) = await SeedTwoTenantsAsync(connection);

        var options = CreateOptions(connection);

        // Two distinct DbContext instances sharing the same EF Core compiled model cache
        await using var contextA = new SqliteTestDbContext(options, new TestTenantService { TenantId = tenantAId });
        await using var contextB = new SqliteTestDbContext(options, new TestTenantService { TenantId = tenantBId });
        await using var contextAnonymous = new SqliteTestDbContext(options, new TestTenantService { TenantId = Guid.Empty });

        Assert.Single(await contextA.Users.ToListAsync());
        Assert.All(await contextA.Users.ToListAsync(), u => Assert.Equal(tenantAId, u.TenantId));
        Assert.Single(await contextA.Customers.ToListAsync());
        Assert.All(await contextA.Customers.ToListAsync(), c => Assert.Equal(tenantAId, c.TenantId));
        Assert.Single(await contextA.Products.ToListAsync());
        Assert.All(await contextA.Products.ToListAsync(), p => Assert.Equal(tenantAId, p.TenantId));
        Assert.Single(await contextA.WorkOrders.ToListAsync());
        Assert.All(await contextA.WorkOrders.ToListAsync(), w => Assert.Equal(tenantAId, w.TenantId));
        Assert.Single(await contextA.WorkOrderProducts.ToListAsync());
        Assert.All(await contextA.WorkOrderProducts.ToListAsync(), wp => Assert.Equal(tenantAId, wp.TenantId));
        Assert.Single(await contextA.WorkOrderAttachments.ToListAsync());
        Assert.All(await contextA.WorkOrderAttachments.ToListAsync(), wa => Assert.Equal(tenantAId, wa.TenantId));
        Assert.Single(await contextA.WorkOrderHistories.ToListAsync());
        Assert.All(await contextA.WorkOrderHistories.ToListAsync(), wh => Assert.Equal(tenantAId, wh.TenantId));

        Assert.Single(await contextB.Users.ToListAsync());
        Assert.All(await contextB.Users.ToListAsync(), u => Assert.Equal(tenantBId, u.TenantId));
        Assert.Single(await contextB.Customers.ToListAsync());
        Assert.All(await contextB.Customers.ToListAsync(), c => Assert.Equal(tenantBId, c.TenantId));
        Assert.Equal(2, (await contextB.Products.ToListAsync()).Count);
        Assert.All(await contextB.Products.ToListAsync(), p => Assert.Equal(tenantBId, p.TenantId));
        Assert.Single(await contextB.WorkOrders.ToListAsync());
        Assert.All(await contextB.WorkOrders.ToListAsync(), w => Assert.Equal(tenantBId, w.TenantId));
        Assert.Single(await contextB.WorkOrderProducts.ToListAsync());
        Assert.All(await contextB.WorkOrderProducts.ToListAsync(), wp => Assert.Equal(tenantBId, wp.TenantId));
        Assert.Single(await contextB.WorkOrderAttachments.ToListAsync());
        Assert.All(await contextB.WorkOrderAttachments.ToListAsync(), wa => Assert.Equal(tenantBId, wa.TenantId));
        Assert.Single(await contextB.WorkOrderHistories.ToListAsync());
        Assert.All(await contextB.WorkOrderHistories.ToListAsync(), wh => Assert.Equal(tenantBId, wh.TenantId));

        Assert.Empty(await contextAnonymous.Users.ToListAsync());
        Assert.Empty(await contextAnonymous.Customers.ToListAsync());
        Assert.Empty(await contextAnonymous.Products.ToListAsync());
        Assert.Empty(await contextAnonymous.WorkOrders.ToListAsync());
        Assert.Empty(await contextAnonymous.WorkOrderProducts.ToListAsync());
        Assert.Empty(await contextAnonymous.WorkOrderAttachments.ToListAsync());
        Assert.Empty(await contextAnonymous.WorkOrderHistories.ToListAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_OnAdded_AssignsAndOverridesTenantIdWithActiveTenant()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var (tenantAId, tenantBId, _, _, _) = await SeedTwoTenantsAsync(connection);

        var options = CreateOptions(connection);
        await using var contextA = new SqliteTestDbContext(options, new TestTenantService { TenantId = tenantAId });

        var newCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            Rut = "77.999.999-9",
            BusinessName = "New Customer",
            MainAddress = "Av. New 1",
            TenantId = Guid.Empty
        };
        var spoofedProduct = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Spoofed Product",
            Price = 500m,
            StockQuantity = 3,
            TenantId = tenantBId
        };

        contextA.Customers.Add(newCustomer);
        contextA.Products.Add(spoofedProduct);
        await contextA.SaveChangesAsync();

        Assert.Equal(tenantAId, newCustomer.TenantId);
        Assert.Equal(tenantAId, spoofedProduct.TenantId);

        await using var contextB = new SqliteTestDbContext(options, new TestTenantService { TenantId = tenantBId });
        Assert.Null(await contextB.Products.FirstOrDefaultAsync(p => p.Id == spoofedProduct.Id));
    }

    [Fact]
    public async Task SaveChangesAsync_OnModified_ThrowsUnauthorizedAccessException_ForCrossTenantOrTamperedTenantId()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var (tenantAId, tenantBId, _, customerAId, customerBId) = await SeedTwoTenantsAsync(connection);

        var options = CreateOptions(connection);
        await using var contextA = new SqliteTestDbContext(options, new TestTenantService { TenantId = tenantAId });

        var foreignCustomer = await contextA.Customers
            .IgnoreQueryFilters()
            .FirstAsync(c => c.Id == customerBId);
        foreignCustomer.BusinessName = "Hijacked Name";

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => contextA.SaveChangesAsync());

        // Verify Tenant B's customer was not mutated in DB
        await using var verifyContextB = new SqliteTestDbContext(options, new TestTenantService { TenantId = tenantBId });
        var unchangedCustomerB = await verifyContextB.Customers.AsNoTracking().FirstAsync(c => c.Id == customerBId);
        Assert.Equal("Client Beta", unchangedCustomerB.BusinessName);

        // Verify tampering TenantId on Tenant A's own entity is also rejected
        await using var tamperContextA = new SqliteTestDbContext(options, new TestTenantService { TenantId = tenantAId });
        var ownCustomer = await tamperContextA.Customers.FirstAsync(c => c.Id == customerAId);
        ownCustomer.TenantId = tenantBId;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tamperContextA.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_OnDeleted_ThrowsUnauthorizedAccessException_ForCrossTenantEntity()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var (tenantAId, tenantBId, standaloneProductBId, _, _) = await SeedTwoTenantsAsync(connection);

        var options = CreateOptions(connection);
        await using var contextA = new SqliteTestDbContext(options, new TestTenantService { TenantId = tenantAId });

        var foreignProduct = await contextA.Products
            .IgnoreQueryFilters()
            .FirstAsync(p => p.Id == standaloneProductBId);
        contextA.Products.Remove(foreignProduct);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => contextA.SaveChangesAsync());

        await using var verifyContextB = new SqliteTestDbContext(options, new TestTenantService { TenantId = tenantBId });
        Assert.NotNull(await verifyContextB.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == standaloneProductBId));
    }

    [Fact]
    public async Task SaveChangesAsync_WhenTenantIdIsEmpty_AllowsExplicitTenantIdsForRegistration()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);

        await using var anonContext = new SqliteTestDbContext(options, new TestTenantService { TenantId = Guid.Empty });
        await anonContext.Database.EnsureCreatedAsync();

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Registered Org",
            TaxId = "76.555.555-5"
        };
        var admin = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Admin User",
            Email = "admin@registered.cl",
            PasswordHash = "hashed",
            Role = "Admin",
            TenantId = tenant.Id
        };

        anonContext.Tenants.Add(tenant);
        anonContext.Users.Add(admin);
        var saved = await anonContext.SaveChangesAsync();

        Assert.Equal(2, saved);
        Assert.Equal(tenant.Id, admin.TenantId);
    }
}
