using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using GearLoop.Api.Dtos;
using GearLoop.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace GearLoop.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly GearLoopDbContext _db;
    private readonly ITokenService _tokenService;

    public AuthController(GearLoopDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var emailTaken = await _db.Users.AnyAsync(u => u.Email == request.Email, cancellationToken);
        if (emailTaken)
        {
            throw new ConflictException($"Email '{request.Email}' is already registered.");
        }

        var user = new AppUser
        {
            FullName = request.FullName,
            Email = request.Email,
            PasswordHash = PasswordHasher.Hash(request.Password),
            Role = UserRole.Customer,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(ToAuthResponse(user));
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials",
                Detail = "Email or password is incorrect."
            });
        }

        return Ok(ToAuthResponse(user));
    }

    private AuthResponse ToAuthResponse(AppUser user)
    {
        var token = _tokenService.CreateToken(user);
        return new AuthResponse(token.Value, token.ExpiresAtUtc, user.FullName, user.Email, user.Role.ToString());
    }
}
