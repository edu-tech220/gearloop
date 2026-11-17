using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GearLoop.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GearLoop.Web.Pages.Account;

public class RegisterModel : PageModel
{
    private readonly GearLoopApiClient _apiClient;

    public RegisterModel(GearLoopApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var auth = await _apiClient.RegisterAsync(Input.FullName, Input.Email, Input.Password);

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, auth.FullName),
                new(ClaimTypes.Email, auth.Email),
                new(ClaimTypes.Role, auth.Role),
                new("access_token", auth.Token)
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { ExpiresUtc = auth.ExpiresAtUtc, IsPersistent = false });

            return LocalRedirect(Url.Content("~/"));
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    public class InputModel
    {
        [Required, MaxLength(120)]
        public string FullName { get; set; } = string.Empty;

        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
