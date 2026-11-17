using GearLoop.Web.Dtos;
using GearLoop.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GearLoop.Web.Pages.Admin;

[Authorize(Roles = "Admin")]
public class WaitlistModel : PageModel
{
    private readonly GearLoopApiClient _apiClient;

    public WaitlistModel(GearLoopApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public PagedResult<WaitlistEntryDto> Entries { get; set; } = new([], 1, 20, 0, 0);

    public async Task OnGetAsync()
    {
        Entries = await _apiClient.GetWaitlistAsync(1, 20);
    }
}
