using System.Linq.Expressions;
using GearLoop.Api.Common;
using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using GearLoop.Api.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GearLoop.Api.Controllers;

[ApiController]
[Route("api/waitlist")]
[Authorize(Roles = "Staff,Admin")]
public class WaitlistController : ControllerBase
{
    private const int MaxPageSize = 50;

    private static readonly Expression<Func<WaitlistEntry, WaitlistEntryDto>> ToDto = w => new WaitlistEntryDto(
        w.Id, w.CustomerId, w.Customer.FullName, w.EquipmentModelId, w.EquipmentModel.Name,
        w.BranchId, w.Branch.Name, w.RequestedStart, w.RequestedEnd, w.Status, w.CreatedAtUtc,
        w.PromotedReservationId);

    private readonly GearLoopDbContext _db;

    public WaitlistController(GearLoopDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<WaitlistEntryDto>>> GetAll(
        [FromQuery] WaitlistStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.WaitlistEntries.AsQueryable();
        if (status is not null)
        {
            query = query.Where(w => w.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(w => w.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<WaitlistEntryDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
    }
}
