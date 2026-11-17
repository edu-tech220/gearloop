namespace GearLoop.Api.Dtos;

public record EquipmentModelDto(
    int Id,
    string Name,
    string Category,
    string Description,
    decimal DailyRate,
    decimal DepositAmount,
    decimal ReplacementValue);

public record AvailabilityResponse(
    int EquipmentModelId,
    int BranchId,
    DateOnly StartDate,
    DateOnly EndDate,
    int AvailableUnits,
    bool CanJoinWaitlist);
