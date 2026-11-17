using System.ComponentModel.DataAnnotations;
using GearLoop.Api.Domain;

namespace GearLoop.Api.Dtos;

public record CreateReservationRequest(
    [Required] int EquipmentModelId,
    [Required] int BranchId,
    [Required] DateOnly StartDate,
    [Required] DateOnly EndDate,
    bool JoinWaitlistIfUnavailable = false);

public record ReturnReservationRequest(
    [Required] DateOnly ActualReturnDate,
    [Required] UnitCondition Condition);

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
