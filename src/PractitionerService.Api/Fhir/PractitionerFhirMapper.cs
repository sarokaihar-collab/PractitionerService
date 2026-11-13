using PractitionerService.Api.Fhir.Models;
using PractitionerService.Application.Common.Exceptions;
using System.Globalization;

namespace PractitionerService.Api.Fhir;

public static class PractitionerFhirMapper
{
    public static FhirPractitionerResource ToFhir(PractitionerReadModel m)
    {
        var resource = new FhirPractitionerResource
        {
            Id = m.Id.ToString(),
            Meta = new FhirMeta
            {
                VersionId = m.Version.ToString(),
                LastUpdated = m.UpdatedAt
            },
            Active = m.IsActive,
            Name = new List<FhirHumanName>
            {
                new() { Family = m.LastName, Given = new List<string> { m.FirstName } }
            },
            Telecom = new List<FhirContactPoint>
            {
                new() { System = "phone", Value = m.Phone },
                new() { System = "email", Value = m.Email }
            },
            Qualification = new List<FhirQualification>
            {
                new() { Code = new FhirCodeableConcept { Text = m.Speciality } }
            },
            Extension = new List<FhirExtension>
{
                new() { Url = FhirConstants.PlaceOfWorkExtensionUrl, ValueString = m.PlaceOfWork },
                new() { Url = FhirConstants.CreatedAtExtensionUrl, ValueString = m.CreatedAt.ToString("O", CultureInfo.InvariantCulture) 
            }}
        };

        if (!string.IsNullOrEmpty(m.SourceEventId))
        {
            resource.Extension.Add(new FhirExtension
            {
                Url = FhirConstants.SourceEventIdExtensionUrl,
                ValueString = m.SourceEventId
            });
        }

        return resource;
    }

    /// <summary>Extracts sourceEventId from extensions (used when a POST originates from the message bus).</summary>
    public static string? ExtractSourceEventId(FhirPractitionerResource fhir) =>
        fhir.Extension?.FirstOrDefault(e => e.Url == FhirConstants.SourceEventIdExtensionUrl)?.ValueString;

    public static FhirBundle ToBundle(PagedResult<PractitionerReadModel> page, PractitionerFilter filter, string selfBaseUrl)
    {
        return new FhirBundle
        {
            Total = page.Total,
            Link = BuildLinks(filter, selfBaseUrl, page.Total),
            Entry = page.Items.Select(item => new FhirBundleEntry
            {
                FullUrl = $"{selfBaseUrl.TrimEnd('/')}/{item.Id}",
                Resource = ToFhir(item),
                Search = new FhirBundleEntrySearch { Mode = "match" }
            }).ToList()
        };
    }

    private static List<FhirBundleLink> BuildLinks(PractitionerFilter filter, string baseUrl, int total)
    {
        var links = new List<FhirBundleLink>
            {
                new() { Relation = "self", Url = BuildSearchUrl(baseUrl, filter, filter.Offset) }
            };

        if (filter.Offset > 0)
        {
            links.Add(new FhirBundleLink
            {
                Relation = "previous",
                Url = BuildSearchUrl(baseUrl, filter, Math.Max(0, filter.Offset - filter.Count))
            });
        }

        if (filter.Offset + filter.Count < total)
        {
            links.Add(new FhirBundleLink
            {
                Relation = "next",
                Url = BuildSearchUrl(baseUrl, filter, filter.Offset + filter.Count)
            });
        }

        return links;
    }

    private static string BuildSearchUrl(string baseUrl, PractitionerFilter f, int offset)
    {
        var parts = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                parts.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
        }

        Add("name", f.NameContains);
        Add("family", f.FamilyContains);
        Add("given", f.GivenContains);
        Add("specialty", f.SpecialityContains);
        Add("place-of-work", f.PlaceOfWorkContains);
        Add("email", f.EmailEquals);
        Add("phone", f.PhoneContains);
        if (f.Active.HasValue) parts.Add($"active={f.Active.Value.ToString().ToLowerInvariant()}");
        if (f.IncludeDeleted) parts.Add("_include-deleted=true");
        parts.Add($"_count={f.Count}");
        parts.Add($"_offset={offset}");

        return $"{baseUrl}?{string.Join("&", parts)}";
    }

    public static CreatePractitionerCommand ToCreateCommand(FhirPractitionerResource fhir, string? sourceEventId)
    {
        ValidateCommon(fhir);

        var (firstName, lastName) = ExtractName(fhir);
        var (phone, email) = ExtractTelecom(fhir);
        var speciality = ExtractSpeciality(fhir);
        var placeOfWork = ExtractPlaceOfWork(fhir);

        return new CreatePractitionerCommand(firstName, lastName, phone, speciality, placeOfWork, email, fhir.Active, sourceEventId);
    }

    public static UpdatePractitionerCommand ToUpdateCommand(Guid id, FhirPractitionerResource fhir, int? expectedVersion)
    {
        ValidateCommon(fhir);

        if (!string.IsNullOrEmpty(fhir.Id) &&
            !Guid.TryParse(fhir.Id, out var bodyId))
        {
            throw new FhirValidationException(FhirConstants.IssueTypeStructure, $"Поле 'id' в теле не является валидным UUID: '{fhir.Id}'.");
        }

        if (Guid.TryParse(fhir.Id, out var parsedId) && parsedId != id)
        {
            throw new FhirValidationException(FhirConstants.IssueTypeStructure, $"Поле 'id' в теле ('{fhir.Id}') не совпадает с id в URL ('{id}').");
        }

        var (firstName, lastName) = ExtractName(fhir);
        var (phone, email) = ExtractTelecom(fhir);
        var speciality = ExtractSpeciality(fhir);
        var placeOfWork = ExtractPlaceOfWork(fhir);

        return new UpdatePractitionerCommand(
            id, firstName, lastName, phone, speciality, placeOfWork, email,
            fhir.Active, expectedVersion);
    }

    private static void ValidateCommon(FhirPractitionerResource fhir)
    {
        if (fhir.ResourceType != FhirConstants.ResourceTypePractitioner)
            throw new FhirValidationException(FhirConstants.IssueTypeStructure, $"Ожидался ресурс '{FhirConstants.ResourceTypePractitioner}', получен '{fhir.ResourceType}'.");
    }

    private static (string FirstName, string LastName) ExtractName(FhirPractitionerResource fhir)
    {
        var name = fhir.Name?.FirstOrDefault();
        if (name is null)
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Поле 'name' обязательно.");

        if (string.IsNullOrWhiteSpace(name.Family))
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Поле 'name[0].family' обязательно.");

        var given = name.Given?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(given))
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Поле 'name[0].given[0]' обязательно.");

        return (given!, name.Family!);
    }

    private static (string Phone, string Email) ExtractTelecom(FhirPractitionerResource fhir)
    {
        if (fhir.Telecom is null || fhir.Telecom.Count == 0)
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Поле 'telecom' обязательно и должно содержать хотя бы один контакт (phone и email).");

        var phone = fhir.Telecom.FirstOrDefault(t => t.System == "phone")?.Value;
        if (string.IsNullOrWhiteSpace(phone))
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Не найден контакт с 'system=phone' в 'telecom'.");

        var email = fhir.Telecom.FirstOrDefault(t => t.System == "email")?.Value;
        if (string.IsNullOrWhiteSpace(email))
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Не найден контакт с 'system=email' в 'telecom'.");

        return (phone!, email!);
    }

    private static string ExtractSpeciality(FhirPractitionerResource fhir) =>
        RequireFhir(() => fhir.Qualification?.FirstOrDefault(q => !string.IsNullOrWhiteSpace(q.Code?.Text))?.Code?.Text, "qualification[0].code.text");

    private static string ExtractPlaceOfWork(FhirPractitionerResource fhir) =>
        RequireFhir(() => fhir.Extension?.FirstOrDefault(e => e.Url == FhirConstants.PlaceOfWorkExtensionUrl)?.ValueString,$"extension[{FhirConstants.PlaceOfWorkExtensionUrl}]");

    /// <summary>
    /// Requires a value to be non-empty; otherwise throws a FHIR validation error
    /// with a reference to the field path.
    /// </summary>
    private static string RequireFhir(Func<string?> getValue, string fieldPath)
    {
        var value = getValue();
        if (string.IsNullOrWhiteSpace(value))
            throw new FhirValidationException(
                FhirConstants.IssueTypeRequired,
                $"Поле '{fieldPath}' обязательно.");
        return value!;
    }
}