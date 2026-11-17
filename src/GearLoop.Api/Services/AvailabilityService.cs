using GearLoop.Api.Data;
using GearLoop.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace GearLoop.Api.Services;

public class AvailabilityService : IAvailabilityService
{
    private static readonly ReservationStatus[] ActiveStatuses =
    {
        ReservationStatus.Confirmed,
        ReservationStatus.CheckedOut
    };

    private readonly GearLoopDbContext _db;

    public AvailabilityService(GearLoopDbContext db)
    {
        _db = db;
    }

    public async Task<int> GetAvailableUnitCountAsync(
        int equipmentModelId,
        int branchId,
        DateOnly startDate,
        DateOnly endDate,
        int? excludeReservationId = null,
        CancellationToken cancellationToken = default)
    {
        var rentableUnitCount = await _db.EquipmentUnits
            .Where(u => u.EquipmentModelId == equipmentModelId
                        && u.BranchId == branchId
                        && u.Status != UnitStatus.Retired
                        && u.Status != UnitStatus.Maintenance)
            .CountAsync(cancellationToken);

        var overlappingReservationCount = await _db.Reservations
            .Where(r => r.EquipmentModelId == equipmentModelId
                        && r.BranchId == branchId
                        && ActiveStatuses.Contains(r.Status)
                        && r.StartDate < endDate
                        && r.EndDate > startDate
                        && (excludeReservationId == null || r.Id != excludeReservationId))
            .CountAsync(cancellationToken);

        return Math.Max(0, rentableUnitCount - overlappingReservationCount);
    }
}
