using GearLoop.Web.Dtos;
using GearLoop.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GearLoop.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public class ReservationsModel : PageModel
{
    private readonly GearLoopApiClient _apiClient;

    public ReservationsModel(GearLoopApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public PagedResult<ReservationDto> Reservations { get; set; } = new([], 1, 20, 0, 0);

    public ReservationStatus? Status { get; set; }

    public async Task OnGetAsync(ReservationStatus? status, int pageNumber = 1)
    {
        Status = status;
        Reservations = await _apiClient.GetAllReservationsAsync(status, Math.Max(1, pageNumber), 20);
    }
}
