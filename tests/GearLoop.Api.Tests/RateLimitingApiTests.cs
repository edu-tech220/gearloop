using System.Net;
using System.Net.Http.Json;
using GearLoop.Api.Dtos;
using GearLoop.Api.Tests.Infrastructure;
using Xunit;

namespace GearLoop.Api.Tests;

/// Uses its own factory with the production auth rate limit intact (every other test class
/// relaxes it to avoid cross-test flakiness).
public class RateLimitingApiTests : IDisposable
{
    private readonly StrictAuthRateLimitApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Login_BeyondThePermitLimit_ReturnsTooManyRequests()
    {
        var client = _factory.CreateClient();

        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)
        {
            last = await client.PostAsJsonAsync("api/auth/login", new LoginRequest("nobody@test.local", "wrong-password"));
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }
}
