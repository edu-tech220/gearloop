namespace GearLoop.Api.Domain;

public enum UserRole
{
    Customer = 0,
    Staff = 1,
    Admin = 2
}

public enum UnitCondition
{
    Good = 0,
    Fair = 1,
    Damaged = 2,
    Retired = 3
}

public enum UnitStatus
{
    Available = 0,
    Reserved = 1,
    CheckedOut = 2,
    Maintenance = 3,
    Retired = 4
}

public enum ReservationStatus
{
    Confirmed = 0,
    CheckedOut = 1,
    Returned = 2,
    Cancelled = 3
}

public enum WaitlistStatus
{
    Waiting = 0,
    Promoted = 1,
    Cancelled = 2
}
