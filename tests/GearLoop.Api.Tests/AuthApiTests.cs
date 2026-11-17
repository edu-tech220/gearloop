using System.Net;
using System.Net.Http.Json;
using GearLoop.Api.Data;
using GearLoop.Api.Dtos;
using GearLoop.Api.Tests.Infrastructure;
using Xunit;

namespace GearLoop.Api.Tests;

public class AuthApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AuthApiTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static string UniqueEmail() => $"{Guid.NewGuid():N}@test.local";

    [Fact]
    public async Task Register_WithNewEmail_ReturnsTokenAndCustomerRole()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("api/auth/register", new RegisterRequest("Jane Doe", UniqueEmail(), "Password123"));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.False(string.IsNullOrWhiteSpace(auth!.Token));
        Assert.Equal("Customer", auth.Role);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();

        await client.PostAsJsonAsync("api/auth/register", new RegisterRequest("Jane Doe", email, "Password123"));
        var second = await client.PostAsJsonAsync("api/auth/register", new RegisterRequest("Jane Doe 2", email, "Password123"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_Succeeds()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(DbSeeder.DemoAdminEmail, DbSeeder.DemoAdminPassword));

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal("Admin", auth!.Role);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        await client.PostAsJsonAsync("api/auth/register", new RegisterRequest("Jane Doe", email, "Password123"));

        var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, "WrongPassword"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
