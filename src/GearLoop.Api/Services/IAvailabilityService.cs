using GearLoop.Api.Domain;

namespace GearLoop.Api.Services;

public interface IAvailabilityService
{
    /// Free capacity for a model/branch over [startDate, endDate): total rentable units minus
    /// active reservations (Confirmed or CheckedOut) whose date range overlaps the request.
    Task<int> GetAvailableUnitCountAsync(
        int equipmentModelId,
        int branchId,
        DateOnly startDate,
        DateOnly endDate,
        int? excludeReservationId = null,
        CancellationToken cancellationToken = default);
}
