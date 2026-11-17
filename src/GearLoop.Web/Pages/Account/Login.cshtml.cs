using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GearLoop.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GearLoop.Web.Pages.Account;

public class LoginModel : PageModel
{
    private readonly GearLoopApiClient _apiClient;

    public LoginModel(GearLoopApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var auth = await _apiClient.LoginAsync(Input.Email, Input.Password);
            await SignInAsync(auth.FullName, auth.Email, auth.Role, auth.Token, auth.ExpiresAtUtc);
            return LocalRedirect(returnUrl ?? Url.Content("~/"));
        }
        catch (ApiException ex) when (ex.StatusCode == 401)
        {
            ErrorMessage = "Email or password is incorrect.";
            return Page();
        }
    }

    private async Task SignInAsync(string fullName, string email, string role, string token, DateTime expiresAtUtc)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, fullName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role),
            new("access_token", token)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { ExpiresUtc = expiresAtUtc, IsPersistent = false });
    }

    public class InputModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
