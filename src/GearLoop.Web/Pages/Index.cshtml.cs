using GearLoop.Web.Dtos;
using GearLoop.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GearLoop.Web.Pages;

public class IndexModel : PageModel
{
    private readonly GearLoopApiClient _apiClient;

    public IndexModel(GearLoopApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public PagedResult<EquipmentModelDto> Models { get; set; } = new([], 1, 9, 0, 0);

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public async Task OnGetAsync()
    {
        Models = await _apiClient.GetEquipmentModelsAsync(Category, Math.Max(1, PageNumber), 9);
    }
}
