using System.Security.Claims;
using FixFlow.API.Services.Tenant;
using FixFlow.Shared.Constants;
using Microsoft.AspNetCore.Http;

namespace FixFlow.API.IntegrationTests;

public sealed class CurrentTenantServiceTests
{
    [Fact]
    public void TenantId_ReturnsParsedGuid_WhenClaimIsValid()
    {
        var expectedTenantId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(CustomClaimTypes.TenantId, expectedTenantId.ToString())
            ]))
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var service = new CurrentTenantService(accessor);

        Assert.Equal(expectedTenantId, service.TenantId);
    }

    [Fact]
    public void TenantId_ReturnsGuidEmpty_WhenHttpContextOrClaimIsMissing()
    {
        var accessorWithoutContext = new HttpContextAccessor { HttpContext = null };
        var serviceWithoutContext = new CurrentTenantService(accessorWithoutContext);
        Assert.Equal(Guid.Empty, serviceWithoutContext.TenantId);

        var accessorWithoutClaim = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var serviceWithoutClaim = new CurrentTenantService(accessorWithoutClaim);
        Assert.Equal(Guid.Empty, serviceWithoutClaim.TenantId);
    }

    [Fact]
    public void TenantId_ReturnsGuidEmpty_WhenClaimValueIsMalformed()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(CustomClaimTypes.TenantId, "not-a-valid-guid")
            ]))
        };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var service = new CurrentTenantService(accessor);

        Assert.Equal(Guid.Empty, service.TenantId);
    }
}
