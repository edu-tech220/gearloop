namespace GearLoop.Api.Domain;

/// Base type for business-rule violations the API layer translates into ProblemDetails responses.
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message)
    {
    }
}

/// Raised for state-machine or capacity conflicts: invalid transitions, no availability, or a
/// concurrent update that could not be reconciled after retrying.
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}
