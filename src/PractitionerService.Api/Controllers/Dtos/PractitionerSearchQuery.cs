using Microsoft.AspNetCore.Mvc;

namespace PractitionerService.Api.Controllers.Dtos;

public sealed class PractitionerSearchQuery
{
    [FromQuery(Name = "name")] public string? Name { get; set; }
    [FromQuery(Name = "family")] public string? Family { get; set; }
    [FromQuery(Name = "given")] public string? Given { get; set; }
    [FromQuery(Name = "active")] public bool? Active { get; set; }
    [FromQuery(Name = "email")] public string? Email { get; set; }
    [FromQuery(Name = "phone")] public string? Phone { get; set; }
    [FromQuery(Name = "specialty")] public string? Specialty { get; set; }
    [FromQuery(Name = "place-of-work")] public string? PlaceOfWork { get; set; }
    [FromQuery(Name = "_count")] public int? Count { get; set; }
    [FromQuery(Name = "_offset")] public int? Offset { get; set; }
    [FromQuery(Name = "_include-deleted")] public bool? IncludeDeleted { get; set; }
}