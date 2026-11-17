using System.ComponentModel.DataAnnotations;

namespace GearLoop.Api.Dtos;

public record RegisterRequest(
    [Required, MaxLength(120)] string FullName,
    [Required, EmailAddress, MaxLength(180)] string Email,
    [Required, MinLength(8)] string Password);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, string FullName, string Email, string Role);
