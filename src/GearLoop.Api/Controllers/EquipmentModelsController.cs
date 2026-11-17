using GearLoop.Api.Common;
using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using GearLoop.Api.Dtos;
using GearLoop.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GearLoop.Api.Controllers;

[ApiController]
[Route("api/equipment-models")]
public class EquipmentModelsController : ControllerBase
{
    private const int MaxPageSize = 50;

    private readonly GearLoopDbContext _db;
    private readonly IAvailabilityService _availability;

    public EquipmentModelsController(GearLoopDbContext db, IAvailabilityService availability)
    {
        _db = db;
        _availability = availability;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<EquipmentModelDto>>> GetAll(
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.EquipmentModels.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(m => m.Category == category);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(m => m.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new EquipmentModelDto(m.Id, m.Name, m.Category, m.Description, m.DailyRate, m.DepositAmount, m.ReplacementValue))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<EquipmentModelDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EquipmentModelDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var model = await _db.EquipmentModels
            .Where(m => m.Id == id)
            .Select(m => new EquipmentModelDto(m.Id, m.Name, m.Category, m.Description, m.DailyRate, m.DepositAmount, m.ReplacementValue))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Equipment model {id} was not found.");

        return Ok(model);
    }

    [HttpGet("{id:int}/availability")]
    public async Task<ActionResult<AvailabilityResponse>> GetAvailability(
        int id,
        [FromQuery] int branchId,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken cancellationToken)
    {
        var modelExists = await _db.EquipmentModels.AnyAsync(m => m.Id == id, cancellationToken);
        if (!modelExists)
        {
            throw new NotFoundException($"Equipment model {id} was not found.");
        }

        var branchExists = await _db.Branches.AnyAsync(b => b.Id == branchId, cancellationToken);
        if (!branchExists)
        {
            throw new NotFoundException($"Branch {branchId} was not found.");
        }

        if (endDate <= startDate)
        {
            throw new ValidationException("endDate must be after startDate.");
        }

        var available = await _availability.GetAvailableUnitCountAsync(id, branchId, startDate, endDate, cancellationToken: cancellationToken);
        return Ok(new AvailabilityResponse(id, branchId, startDate, endDate, available, available <= 0));
    }
}
