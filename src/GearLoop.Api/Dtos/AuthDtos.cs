using System.ComponentModel.DataAnnotations;

namespace GearLoop.Api.Dtos;

public record RegisterRequest(
    [property: Required, MaxLength(120)] string FullName,
    [property: Required, EmailAddress, MaxLength(180)] string Email,
    [property: Required, MinLength(8)] string Password);

public record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, string FullName, string Email, string Role);
