using System.ComponentModel.DataAnnotations;
using GearLoop.Web.Dtos;
using GearLoop.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GearLoop.Web.Pages.Models;

public class DetailsModel : PageModel
{
    private readonly GearLoopApiClient _apiClient;

    public DetailsModel(GearLoopApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public EquipmentModelDto EquipmentModel { get; set; } = null!;
    public List<BranchDto> Branches { get; set; } = new();
    public AvailabilityResponse? Availability { get; set; }
    public string? ErrorMessage { get; set; }
    public string? InfoMessage { get; set; }

    [BindProperty]
    public ReserveInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await TryLoadAsync(id))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCheckAvailabilityAsync(int id)
    {
        if (!await TryLoadAsync(id))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        Availability = await _apiClient.GetAvailabilityAsync(id, Input.BranchId, Input.StartDate, Input.EndDate);
        return Page();
    }

    public async Task<IActionResult> OnPostReserveAsync(int id)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("/Models/Details", new { id }) });
        }

        if (!await TryLoadAsync(id))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var result = await _apiClient.CreateReservationAsync(
                id, Input.BranchId, Input.StartDate, Input.EndDate, Input.JoinWaitlistIfUnavailable);

            if (result.Reservation is not null)
            {
                return RedirectToPage("/Reservations/Index");
            }

            InfoMessage = "No units were available for those dates, so you were added to the waitlist. " +
                          "A reservation will be confirmed automatically as soon as one frees up.";
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }

        return Page();
    }

    private async Task<bool> TryLoadAsync(int id)
    {
        try
        {
            EquipmentModel = await _apiClient.GetEquipmentModelAsync(id);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return false;
        }

        Branches = await _apiClient.GetBranchesAsync();
        return true;
    }

    public class ReserveInput
    {
        [Required]
        public int BranchId { get; set; }

        [Required, DataType(DataType.Date)]
        public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        [Required, DataType(DataType.Date)]
        public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

        public bool JoinWaitlistIfUnavailable { get; set; }
    }
}
