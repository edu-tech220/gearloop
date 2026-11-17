using GearLoop.Api.Domain;

namespace GearLoop.Api.Dtos;

public record WaitlistEntryDto(
    int Id,
    int CustomerId,
    string CustomerName,
    int EquipmentModelId,
    string EquipmentModelName,
    int BranchId,
    string BranchName,
    DateOnly RequestedStart,
    DateOnly RequestedEnd,
    WaitlistStatus Status,
    DateTime CreatedAtUtc,
    int? PromotedReservationId);
