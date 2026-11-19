using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using FixFlow.API.Data;
using FixFlow.API.IntegrationTests.Infrastructure;
using FixFlow.Shared.Constants;
using FixFlow.Shared.Dtos.Auth.Login;
using FixFlow.Shared.Dtos.Auth.Register;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FixFlow.API.IntegrationTests;

public sealed class AuthEndpointsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ReturnsOk_AndConflictOnDuplicateEmail()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registerRequest = new RegisterTenantRequestDto
        {
            CompanyName = $"Empresa {uniqueId}",
            CompanyRut = $"76.{uniqueId}-1",
            Services = "Mantención",
            FullName = "Admin Test",
            Email = $"admin-{uniqueId}@fixflow.cl",
            Password = "Password123!",
            UserRut = "12.345.678-9"
        };

        var firstResponse = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var duplicateResponse = await _client.PostAsJsonAsync("/api/Auth/register", registerRequest);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsOkWithJwtContainingTenantIdClaim_WhenCredentialsAreValid()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var email = $"login-{uniqueId}@fixflow.cl";
        const string password = "Password123!";

        var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", new RegisterTenantRequestDto
        {
            CompanyName = $"Tenant Login {uniqueId}",
            CompanyRut = $"77.{uniqueId}-2",
            FullName = "Login Admin",
            Email = email,
            Password = password
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var payload = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload.Token));
        Assert.True(Guid.TryParse(payload.TenantId, out var parsedTenantId));
        Assert.NotEqual(Guid.Empty, parsedTenantId);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(payload.Token);
        var tenantClaim = jwt.Claims.FirstOrDefault(c => c.Type == CustomClaimTypes.TenantId);
        Assert.NotNull(tenantClaim);
        Assert.Equal(payload.TenantId, tenantClaim.Value);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIsIncorrect()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var email = $"badpass-{uniqueId}@fixflow.cl";

        var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", new RegisterTenantRequestDto
        {
            CompanyName = $"Tenant BadPass {uniqueId}",
            CompanyRut = $"78.{uniqueId}-3",
            FullName = "BadPass Admin",
            Email = email,
            Password = "CorrectPassword123!"
        });
        registerResponse.EnsureSuccessStatusCode();

        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequestDto
        {
            Email = email,
            Password = "WrongPassword!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    [Fact]
    public async Task Login_ReturnsForbidden_WhenTenantIsInactive()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var email = $"inactive-{uniqueId}@fixflow.cl";
        const string password = "Password123!";

        var registerResponse = await _client.PostAsJsonAsync("/api/Auth/register", new RegisterTenantRequestDto
        {
            CompanyName = $"Tenant Inactive {uniqueId}",
            CompanyRut = $"79.{uniqueId}-4",
            FullName = "Inactive Admin",
            Email = email,
            Password = password
        });
        registerResponse.EnsureSuccessStatusCode();

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.IgnoreQueryFilters().Include(u => u.Tenant).FirstAsync(u => u.Email == email);
            user.Tenant!.IsActive = false;
            await db.SaveChangesAsync();
        }

        var loginResponse = await _client.PostAsJsonAsync("/api/Auth/login", new LoginRequestDto
        {
            Email = email,
            Password = password
        });

        Assert.Equal(HttpStatusCode.Forbidden, loginResponse.StatusCode);
    }
}
