using GearLoop.Api.Domain;

namespace GearLoop.Api.Services;

public record ReservationCreationResult(Reservation? Reservation, WaitlistEntry? WaitlistEntry);

public interface IReservationService
{
    Task<ReservationCreationResult> CreateReservationAsync(
        int customerId,
        int equipmentModelId,
        int branchId,
        DateOnly startDate,
        DateOnly endDate,
        bool joinWaitlistIfUnavailable,
        CancellationToken cancellationToken = default);

    Task<Reservation> CancelReservationAsync(
        int reservationId,
        int? requestingCustomerId,
        CancellationToken cancellationToken = default);

    Task<Reservation> CheckoutAsync(int reservationId, CancellationToken cancellationToken = default);

    Task<Reservation> ReturnAsync(
        int reservationId,
        DateOnly actualReturnDate,
        UnitCondition condition,
        CancellationToken cancellationToken = default);
}
