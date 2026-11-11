namespace PractitionerService.Application.Common.Exceptions;

/// <summary>
/// Identifies which unique constraint was violated, without leaking DB-specific names.
/// </summary>
public enum UniqueConstraintField
{
    Unknown,
    Email,
    Phone,
    SourceEventId
}

/// <summary>
/// Violation of a unique DB constraint. Infrastructure translates the provider-specific
/// exception into this type, resolving the field and producing a user-friendly message.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintField Field { get; }
    public string? ConstraintName { get; }

    public UniqueConstraintViolationException( UniqueConstraintField field, string message, string? constraintName = null, Exception? inner = null) : base(message, inner)
    {
        Field = field;
        ConstraintName = constraintName;
    }
}