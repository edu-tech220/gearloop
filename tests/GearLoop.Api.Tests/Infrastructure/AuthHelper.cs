using System.Net.Http.Headers;
using System.Net.Http.Json;
using GearLoop.Api.Dtos;

namespace GearLoop.Api.Tests.Infrastructure;

public static class AuthHelper
{
    public static async Task<string> RegisterAsync(this HttpClient client, string? email = null, string? fullName = null)
    {
        var response = await client.PostAsJsonAsync("api/auth/register", new RegisterRequest(
            fullName ?? "Test Customer", email ?? $"{Guid.NewGuid():N}@test.local", "Password123"));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    public static async Task<string> LoginAsync(this HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return auth!.Token;
    }

    public static void UseBearerToken(this HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
