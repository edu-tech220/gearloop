using GearLoop.Api.Domain;

namespace GearLoop.Api.Security;

public record AccessToken(string Value, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateToken(AppUser user);
}
