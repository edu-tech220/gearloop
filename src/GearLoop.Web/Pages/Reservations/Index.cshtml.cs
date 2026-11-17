using GearLoop.Web.Dtos;
using GearLoop.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GearLoop.Web.Pages.Reservations;

[Authorize]
public class IndexModel : PageModel
{
    private readonly GearLoopApiClient _apiClient;

    public IndexModel(GearLoopApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public PagedResult<ReservationDto> Reservations { get; set; } = new([], 1, 20, 0, 0);
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        Reservations = await _apiClient.GetMyReservationsAsync(1, 20);
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        try
        {
            await _apiClient.CancelReservationAsync(id);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        Reservations = await _apiClient.GetMyReservationsAsync(1, 20);
        return Page();
    }
}
