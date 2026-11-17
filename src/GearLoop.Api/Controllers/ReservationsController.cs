using System.Linq.Expressions;
using System.Security.Claims;
using GearLoop.Api.Common;
using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using GearLoop.Api.Dtos;
using GearLoop.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GearLoop.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private const int MaxPageSize = 50;

    private static readonly Expression<Func<Reservation, ReservationDto>> ToDto = r => new ReservationDto(
        r.Id,
        r.CustomerId,
        r.Customer.FullName,
        r.EquipmentModelId,
        r.EquipmentModel.Name,
        r.BranchId,
        r.Branch.Name,
        r.StartDate,
        r.EndDate,
        r.Status,
        r.AssignedUnitId,
        r.CreatedAtUtc,
        r.Charge == null
            ? null
            : new RentalChargeDto(
                r.Charge.BaseAmount, r.Charge.DiscountAmount, r.Charge.SurchargeAmount,
                r.Charge.LateFeeAmount, r.Charge.DamageFeeAmount,
                r.Charge.DepositAmount, r.Charge.DepositRefunded, r.Charge.TotalAmount));

    private static readonly Expression<Func<WaitlistEntry, WaitlistEntryDto>> ToWaitlistDto = w => new WaitlistEntryDto(
        w.Id, w.CustomerId, w.Customer.FullName, w.EquipmentModelId, w.EquipmentModel.Name,
        w.BranchId, w.Branch.Name, w.RequestedStart, w.RequestedEnd, w.Status, w.CreatedAtUtc,
        w.PromotedReservationId);

    private readonly GearLoopDbContext _db;
    private readonly IReservationService _reservations;

    public ReservationsController(GearLoopDbContext db, IReservationService reservations)
    {
        _db = db;
        _reservations = reservations;
    }

    [HttpPost]
    public async Task<ActionResult<ReservationCreationResponse>> Create(
        CreateReservationRequest request, CancellationToken cancellationToken)
    {
        var customerId = GetUserId();
        var result = await _reservations.CreateReservationAsync(
            customerId, request.EquipmentModelId, request.BranchId,
            request.StartDate, request.EndDate, request.JoinWaitlistIfUnavailable, cancellationToken);

        if (result.Reservation is not null)
        {
            var dto = await _db.Reservations.Where(r => r.Id == result.Reservation.Id).Select(ToDto).FirstAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, new ReservationCreationResponse(dto, null));
        }

        var waitlistDto = await _db.WaitlistEntries
            .Where(w => w.Id == result.WaitlistEntry!.Id)
            .Select(ToWaitlistDto)
            .FirstAsync(cancellationToken);
        return Accepted(new ReservationCreationResponse(null, waitlistDto));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<ReservationDto>>> GetMine(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var customerId = GetUserId();
        return Ok(await PageAsync(_db.Reservations.Where(r => r.CustomerId == customerId), page, pageSize, cancellationToken));
    }

    [HttpGet]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<ActionResult<PagedResult<ReservationDto>>> GetAll(
        [FromQuery] ReservationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Reservations.AsQueryable();
        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }

        return Ok(await PageAsync(query, page, pageSize, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ReservationDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var reservation = await _db.Reservations.Where(r => r.Id == id).Select(ToDto).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Reservation {id} was not found.");

        if (!IsStaffOrAdmin() && reservation.CustomerId != GetUserId())
        {
            throw new NotFoundException($"Reservation {id} was not found.");
        }

        return Ok(reservation);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ReservationDto>> Cancel(int id, CancellationToken cancellationToken)
    {
        var requestingCustomerId = IsStaffOrAdmin() ? (int?)null : GetUserId();
        await _reservations.CancelReservationAsync(id, requestingCustomerId, cancellationToken);
        var dto = await _db.Reservations.Where(r => r.Id == id).Select(ToDto).FirstAsync(cancellationToken);
        return Ok(dto);
    }

    [HttpPost("{id:int}/checkout")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<ActionResult<ReservationDto>> Checkout(int id, CancellationToken cancellationToken)
    {
        await _reservations.CheckoutAsync(id, cancellationToken);
        var dto = await _db.Reservations.Where(r => r.Id == id).Select(ToDto).FirstAsync(cancellationToken);
        return Ok(dto);
    }

    [HttpPost("{id:int}/return")]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<ActionResult<ReservationDto>> Return(
        int id, ReturnReservationRequest request, CancellationToken cancellationToken)
    {
        await _reservations.ReturnAsync(id, request.ActualReturnDate, request.Condition, cancellationToken);
        var dto = await _db.Reservations.Where(r => r.Id == id).Select(ToDto).FirstAsync(cancellationToken);
        return Ok(dto);
    }

    private async Task<PagedResult<ReservationDto>> PageAsync(
        IQueryable<Reservation> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

        return new PagedResult<ReservationDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub")!);

    private bool IsStaffOrAdmin() => User.IsInRole(nameof(UserRole.Staff)) || User.IsInRole(nameof(UserRole.Admin));
}
