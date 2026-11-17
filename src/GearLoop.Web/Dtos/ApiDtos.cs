namespace GearLoop.Web.Dtos;

public record BranchDto(int Id, string Name, string City, string Address);

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

public record AuthResponse(string Token, DateTime ExpiresAtUtc, string FullName, string Email, string Role);

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

public record ReservationCreationResponse(ReservationDto? Reservation, WaitlistEntryDto? WaitlistEntry);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
