using System.ComponentModel.DataAnnotations;

namespace PractitionerService.Infrastructure.Options;

/// <summary>
/// Database connection options. Bound from the "ConnectionStrings" configuration section.
/// </summary>
public sealed class DatabaseOptions
{
    public const string Section = "ConnectionStrings";

    /// <summary>Default EF Core connection string for the practitioner database.</summary>
    [Required(ErrorMessage = "Строка подключения к БД (ConnectionStrings:Default) обязательна.")]
    public string Default { get; set; } = string.Empty;
}