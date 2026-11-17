namespace GearLoop.Web.Dtos;

public enum ReservationStatus
{
    Confirmed = 0,
    CheckedOut = 1,
    Returned = 2,
    Cancelled = 3
}

public enum UnitCondition
{
    Good = 0,
    Fair = 1,
    Damaged = 2,
    Retired = 3
}

public enum WaitlistStatus
{
    Waiting = 0,
    Promoted = 1,
    Cancelled = 2
}
