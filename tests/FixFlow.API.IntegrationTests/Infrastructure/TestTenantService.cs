using FixFlow.API.Services.Tenant;

namespace FixFlow.API.IntegrationTests.Infrastructure;

public sealed class TestTenantService : ICurrentTenantService
{
    public Guid TenantId { get; set; } = Guid.Empty;
}
