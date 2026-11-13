using Microsoft.AspNetCore.Mvc;

namespace PractitionerService.Api.Controllers.Contracts;

/// <summary>
/// Параметры поиска врачей. Все параметры опциональны.
/// </summary>
public sealed class PractitionerSearchQuery
{
    /// <summary>
    /// Часть ФИО. Ищет по firstName и lastName одновременно, без учёта регистра.
    /// </summary>
    [FromQuery(Name = "name")]
    public string? Name { get; set; }

    /// <summary>
    /// Часть фамилии, без учёта регистра.
    /// </summary>
    [FromQuery(Name = "family")]
    public string? Family { get; set; }

    /// <summary>
    /// Часть имени, без учёта регистра.
    /// </summary>
    [FromQuery(Name = "given")]
    public string? Given { get; set; }

    /// <summary>
    /// Признак активности: true — только активные, false — только неактивные, пусто — все.
    /// </summary>
    [FromQuery(Name = "active")]
    public bool? Active { get; set; }

    /// <summary>
    /// Точное совпадение email (без учёта регистра).
    /// </summary>
    [FromQuery(Name = "email")]
    public string? Email { get; set; }

    /// <summary>
    /// Часть номера телефона.
    /// </summary>
    [FromQuery(Name = "phone")]
    public string? Phone { get; set; }

    /// <summary>
    /// Часть специальности, без учёта регистра.
    /// </summary>
    [FromQuery(Name = "specialty")]
    public string? Specialty { get; set; }

    /// <summary>
    /// Часть места работы, без учёта регистра.
    /// </summary>
    [FromQuery(Name = "place-of-work")]
    public string? PlaceOfWork { get; set; }

    /// <summary>
    /// Размер страницы (1–100). По умолчанию 20.
    /// </summary>
    [FromQuery(Name = "_count")]
    public int? Count { get; set; }

    /// <summary>
    /// Смещение от начала списка. По умолчанию 0.
    /// </summary>
    [FromQuery(Name = "_offset")]
    public int? Offset { get; set; }

    /// <summary>
    /// Включать в результат soft-deleted записи. По умолчанию false. Кастомный параметр, не из спецификации FHIR.
    /// </summary>
    [FromQuery(Name = "_include-deleted")]
    public bool? IncludeDeleted { get; set; }
}
