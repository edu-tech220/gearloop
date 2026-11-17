using GearLoop.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GearLoop.Api.Tests.Infrastructure;

/// Boots the real Api host (controllers, auth, exception handling, DI wiring) against an
/// in-memory SQLite database instead of SQL Server, so the whole request pipeline - not just
/// the service layer - is covered without any external dependency.
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public ApiFactory()
    {
        _connection.Open();
    }

    /// A whole test class can register/log in far more than 5 times a minute; most tests raise
    /// the auth rate limit instead of weakening the production default. RateLimitingApiTests
    /// overrides this to exercise the real limit.
    protected virtual bool RelaxAuthRateLimit => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (RelaxAuthRateLimit)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Auth:PermitLimit"] = "1000",
                ["RateLimiting:Auth:WindowSeconds"] = "60"
            }));
        }

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<GearLoopDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<GearLoopDbContext>(options => options.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
