namespace PractitionerService.Application.Practitioners.Events;

public sealed class PractitionerImportPayload
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Speciality { get; set; } = string.Empty;
    public string PlaceOfWork { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool? IsActive { get; set; }
}
