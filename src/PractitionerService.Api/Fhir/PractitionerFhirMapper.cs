using System.Globalization;
using PractitionerService.Api.Fhir.Models;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Exceptions;
using PractitionerService.Application.Practitioners;

namespace PractitionerService.Api.Fhir;

public static class PractitionerFhirMapper
{
    public static FhirPractitionerResource ToFhir(PractitionerReadModel m)
    {
        ArgumentNullException.ThrowIfNull(m);

        return new FhirPractitionerResource
        {
            Id = m.Id.ToString(),
            Meta = BuildMeta(m),
            Contained = new()
        {
            new FhirOrganization
            {
                Id = FhirConstants.ContainedOrganizationId,
                Name = m.PlaceOfWork,
            },
        },
            Active = m.IsActive,
            Name = new() { new() { Family = m.LastName, Given = new() { m.FirstName } } },
            Telecom = new()
        {
            new() { System = FhirConstants.TelecomSystemPhone, Value = m.Phone },
            new() { System = FhirConstants.TelecomSystemEmail, Value = m.Email },
        },
            Qualification = new()
        {
            new()
            {
                Code = new FhirCodeableConcept { Text = m.Speciality },
                Issuer = new FhirReference
                {
                    Reference = $"#{FhirConstants.ContainedOrganizationId}",
                },
            },
        },
        };
    }

    /// <summary>Extracts sourceEventId from extensions (used when a POST originates from the message bus).</summary>
    public static string? ExtractSourceEventId(FhirPractitionerResource fhir)
    {
        ArgumentNullException.ThrowIfNull(fhir);

        return fhir.Meta?.Tag?.FirstOrDefault(t => string.Equals(t.System, FhirConstants.MetaTagSystemSourceEventId, StringComparison.Ordinal))?.Code;
    }

    public static FhirBundle ToBundle(PagedResult<PractitionerReadModel> page, PractitionerFilter filter, string selfBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(selfBaseUrl);

        return new FhirBundle
        {
            Total = page.Total,
            Link = BuildLinks(filter, selfBaseUrl, page.Total),
            Entry = page.Items.Select(item => new FhirBundleEntry
            {
                FullUrl = $"{selfBaseUrl.TrimEnd('/')}/{item.Id}",
                Resource = ToFhir(item),
                Search = new FhirBundleEntrySearch { Mode = FhirConstants.BundleEntryModeMatch },
            }).ToList(),
        };
    }

    public static CreatePractitionerCommand ToCreateCommand(FhirPractitionerResource fhir, string? sourceEventId)
    {
        ArgumentNullException.ThrowIfNull(fhir);

        Validate(fhir);

        var (firstName, lastName) = ExtractName(fhir);
        var (phone, email) = ExtractTelecom(fhir);
        var speciality = ExtractSpeciality(fhir);
        var placeOfWork = ExtractPlaceOfWork(fhir);

        return new CreatePractitionerCommand(firstName, lastName, phone, speciality, placeOfWork, email, fhir.Active, sourceEventId);
    }

    public static UpdatePractitionerCommand ToUpdateCommand(Guid id, FhirPractitionerResource fhir, int? expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(fhir);

        Validate(fhir);
        if (!string.IsNullOrEmpty(fhir.Id) &&
            !Guid.TryParse(fhir.Id, out _))
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

        return new UpdatePractitionerCommand(id, firstName, lastName, phone, speciality, placeOfWork, email, fhir.Active, expectedVersion);
    }

    private static FhirMeta BuildMeta(PractitionerReadModel m)
    {
        var tags = new List<FhirCoding>
    {
        new()
        {
            System = FhirConstants.MetaTagSystemCreatedAt,
            Code = m.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        },
    };

        if (!string.IsNullOrEmpty(m.SourceEventId))
        {
            tags.Add(new FhirCoding
            {
                System = FhirConstants.MetaTagSystemSourceEventId,
                Code = m.SourceEventId,
            });
        }

        return new FhirMeta
        {
            VersionId = m.Version.ToString(),
            LastUpdated = m.UpdatedAt,
            Tag = tags,
        };
    }

    private static string ExtractPlaceOfWork(FhirPractitionerResource fhir)
    {
        var issuerRef = fhir.Qualification?
            .FirstOrDefault(q => !string.IsNullOrWhiteSpace(q.Code?.Text))?
            .Issuer?
            .Reference;

        if (string.IsNullOrWhiteSpace(issuerRef) || !issuerRef.StartsWith('#'))
        {
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, $"Поле 'qualification[0].issuer.reference' обязательно и должно указывать на contained Organization (например, '#{FhirConstants.ContainedOrganizationId}').");
        }

        var containedId = issuerRef[1..];
        var org = fhir.Contained?.FirstOrDefault(o =>
            string.Equals(o.Id, containedId, StringComparison.Ordinal));

        if (org is null || string.IsNullOrWhiteSpace(org.Name))
        {
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, $"Contained Organization с id '{containedId}' не найдена или не содержит 'name'.");
        }

        return org.Name;
    }

    private static List<FhirBundleLink> BuildLinks(PractitionerFilter filter, string baseUrl, int total)
    {
        var links = new List<FhirBundleLink>
            {
                new() { Relation = FhirConstants.LinkRelationSelf, Url = BuildSearchUrl(baseUrl, filter, filter.Offset) },
            };

        if (filter.Offset > 0)
        {
            links.Add(new FhirBundleLink
            {
                Relation = FhirConstants.LinkRelationPrevious,
                Url = BuildSearchUrl(baseUrl, filter, Math.Max(0, filter.Offset - filter.Count)),
            });
        }

        if (filter.Offset + filter.Count < total)
        {
            links.Add(new FhirBundleLink
            {
                Relation = FhirConstants.LinkRelationNext,
                Url = BuildSearchUrl(baseUrl, filter, filter.Offset + filter.Count),
            });
        }

        return links;
    }

    private static string BuildSearchUrl(string baseUrl, PractitionerFilter filter, int offset)
    {
        var parts = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={Uri.EscapeDataString(value.Trim())}");
            }
        }

        Add("name", filter.NameContains);
        Add("family", filter.FamilyContains);
        Add("given", filter.GivenContains);
        Add("specialty", filter.SpecialityContains);
        Add("place-of-work", filter.PlaceOfWorkContains);
        Add("email", filter.EmailEquals);
        Add("phone", filter.PhoneContains);
        if (filter.Active.HasValue)
        {
            parts.Add($"active={filter.Active.Value.ToString().ToLowerInvariant()}");
        }

        if (filter.IncludeDeleted)
        {
            parts.Add("_include-deleted=true");
        }

        parts.Add($"_count={filter.Count}");
        parts.Add($"_offset={offset}");

        return $"{baseUrl}?{string.Join("&", parts)}";
    }

    private static void Validate(FhirPractitionerResource fhir)
    {
        if (!string.Equals(fhir.ResourceType, FhirConstants.ResourceTypePractitioner, StringComparison.Ordinal))
        {
            throw new FhirValidationException(FhirConstants.IssueTypeStructure, $"Ожидался ресурс '{FhirConstants.ResourceTypePractitioner}', получен '{fhir.ResourceType}'.");
        }
    }

    private static (string FirstName, string LastName) ExtractName(FhirPractitionerResource fhir)
    {
        var name = fhir.Name?.FirstOrDefault();
        if (name is null)
        {
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Поле 'name' обязательно.");
        }

        if (string.IsNullOrWhiteSpace(name.Family))
        {
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Поле 'name[0].family' обязательно.");
        }

        var given = name.Given?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(given))
        {
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Поле 'name[0].given[0]' обязательно.");
        }

        return (given, name.Family);
    }

    private static (string Phone, string Email) ExtractTelecom(FhirPractitionerResource fhir)
    {
        if (fhir.Telecom is null || fhir.Telecom.Count == 0)
        {
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Поле 'telecom' обязательно и должно содержать хотя бы один контакт (phone и email).");
        }

        var phone = fhir.Telecom.FirstOrDefault(t => string.Equals(t.System, FhirConstants.TelecomSystemPhone, StringComparison.Ordinal))?.Value;
        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Не найден контакт с 'system=phone' в 'telecom'.");
        }

        var email = fhir.Telecom.FirstOrDefault(t => string.Equals(t.System, FhirConstants.TelecomSystemEmail, StringComparison.Ordinal))?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, "Не найден контакт с 'system=email' в 'telecom'.");
        }

        return (phone, email);
    }

    private static string ExtractSpeciality(FhirPractitionerResource fhir) =>
        RequireFhir(() => fhir.Qualification?.FirstOrDefault(q => !string.IsNullOrWhiteSpace(q.Code?.Text))?.Code?.Text, "qualification[0].code.text");

    /// <summary>
    /// Requires a value to be non-empty; otherwise throws a FHIR validation error
    /// with a reference to the field path.
    /// </summary>
    private static string RequireFhir(Func<string?> getValue, string fieldPath)
    {
        var value = getValue();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FhirValidationException(FhirConstants.IssueTypeRequired, $"Поле '{fieldPath}' обязательно.");
        }

        return value;
    }
}
