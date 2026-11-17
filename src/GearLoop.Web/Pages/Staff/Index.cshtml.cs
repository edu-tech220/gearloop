using GearLoop.Web.Dtos;
using GearLoop.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GearLoop.Web.Pages.Staff;

[Authorize(Roles = "Staff,Admin")]
public class IndexModel : PageModel
{
    private readonly GearLoopApiClient _apiClient;

    public IndexModel(GearLoopApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public List<ReservationDto> AwaitingCheckout { get; set; } = new();
    public List<ReservationDto> AwaitingReturn { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostCheckoutAsync(int id)
    {
        try
        {
            await _apiClient.CheckoutAsync(id);
            StatusMessage = $"Reservation #{id} checked out.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostReturnAsync(int id, DateOnly actualReturnDate, UnitCondition condition)
    {
        try
        {
            await _apiClient.ReturnAsync(id, actualReturnDate, condition);
            StatusMessage = $"Reservation #{id} marked as returned.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var confirmed = await _apiClient.GetAllReservationsAsync(ReservationStatus.Confirmed, 1, 50);
        var checkedOut = await _apiClient.GetAllReservationsAsync(ReservationStatus.CheckedOut, 1, 50);
        AwaitingCheckout = confirmed.Items.ToList();
        AwaitingReturn = checkedOut.Items.ToList();
    }
}
