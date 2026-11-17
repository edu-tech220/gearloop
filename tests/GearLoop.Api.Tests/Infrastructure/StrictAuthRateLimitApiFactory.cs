namespace GearLoop.Api.Tests.Infrastructure;

/// Keeps the production auth rate limit intact (5 requests/minute) to verify the limiter itself.
public class StrictAuthRateLimitApiFactory : ApiFactory
{
    protected override bool RelaxAuthRateLimit => false;
}
