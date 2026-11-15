namespace PractitionerService.Application.Common;

/// <summary>
/// Abstracts the system clock. Used by command handlers to set
/// <c>CreatedAt</c> / <c>UpdatedAt</c> consistently and test deterministically.
/// </summary>
public interface IDateTimeProvider
{
    /// <summary>Current UTC time.</summary>
    DateTime UtcNow { get; }
}
