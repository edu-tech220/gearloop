using System.ComponentModel.DataAnnotations;
using GearLoop.Api.Domain;

namespace GearLoop.Api.Dtos;

public record CreateReservationRequest(
    [property: Required] int EquipmentModelId,
    [property: Required] int BranchId,
    [property: Required] DateOnly StartDate,
    [property: Required] DateOnly EndDate,
    bool JoinWaitlistIfUnavailable = false);

public record ReturnReservationRequest(
    [property: Required] DateOnly ActualReturnDate,
    [property: Required] UnitCondition Condition);

public record RentalChargeDto(
    decimal BaseAmount,
    decimal DiscountAmount,
    decimal SurchargeAmount,
    decimal LateFeeAmount,
    decimal DamageFeeAmount,
    decimal DepositAmount,
    decimal DepositRefunded,
    decimal TotalAmount);

public record ReservationDto(
    int Id,
    int CustomerId,
    string CustomerName,
    int EquipmentModelId,
    string EquipmentModelName,
    int BranchId,
    string BranchName,
    DateOnly StartDate,
    DateOnly EndDate,
    ReservationStatus Status,
    int? AssignedUnitId,
    DateTime CreatedAtUtc,
    RentalChargeDto? Charge);

public record ReservationCreationResponse(ReservationDto? Reservation, WaitlistEntryDto? WaitlistEntry);
