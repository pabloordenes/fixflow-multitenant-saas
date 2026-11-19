using FixFlow.Shared.Constants;

namespace FixFlow.API.Services.Tenant;

public interface ICurrentTenantService
{
    Guid TenantId { get; }
}

public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid TenantId
    {
        get
        {
            var tenantClaim = _httpContextAccessor.HttpContext?.User.FindFirst(CustomClaimTypes.TenantId)?.Value;
            return Guid.TryParse(tenantClaim, out var tenantId) ? tenantId : Guid.Empty;
        }
    }
}
