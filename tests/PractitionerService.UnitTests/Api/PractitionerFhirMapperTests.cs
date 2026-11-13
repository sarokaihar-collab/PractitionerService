using FluentAssertions;
using PractitionerService.Api.Fhir;
using PractitionerService.Api.Fhir.Models;
using PractitionerService.Application.Common.Exceptions;

namespace PractitionerService.UnitTests.Api;

public sealed class PractitionerFhirMapperTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static PractitionerReadModel NewReadModel() => new(
        Guid.NewGuid(), "Иван", "Иванов", "+7-900-000-00-00",
        "Кардиолог", "ГКБ №1", "i@example.ru", true,
        Now, Now, 3, "evt-1");

    private static FhirPractitionerResource NewFhirResource() => new()
    {
        ResourceType = "Practitioner",
        Active = true,
        Name = new() { new() { Family = "Иванов", Given = new() { "Иван" } } },
        Telecom = new()
        {
            new() { System = "phone", Value = "+7-900-000-00-00" },
            new() { System = "email", Value = "i@example.ru" }
        },
        Qualification = new() { new() { Code = new() { Text = "Кардиолог" } } },
        Extension = new()
        {
            new() { Url = FhirConstants.PlaceOfWorkExtensionUrl, ValueString = "ГКБ №1" }
        }
    };

    // ==== ToFhir ====

    [Fact]
    public void ToFhir_MapsAllFields()
    {
        var rm = NewReadModel();

        var fhir = PractitionerFhirMapper.ToFhir(rm);

        fhir.ResourceType.Should().Be("Practitioner");
        fhir.Id.Should().Be(rm.Id.ToString());
        fhir.Active.Should().BeTrue();
        fhir.Meta!.VersionId.Should().Be("3");
        fhir.Meta.LastUpdated.Should().Be(rm.UpdatedAt);
        fhir.Name!.Single().Family.Should().Be("Иванов");
        fhir.Name.Single().Given!.Single().Should().Be("Иван");
        fhir.Telecom!.Should().Contain(t => t.System == "phone" && t.Value == "+7-900-000-00-00");
        fhir.Telecom!.Should().Contain(t => t.System == "email" && t.Value == "i@example.ru");
        fhir.Qualification!.Single().Code!.Text.Should().Be("Кардиолог");
        fhir.Extension!.Should().Contain(e =>
            e.Url == FhirConstants.PlaceOfWorkExtensionUrl && e.ValueString == "ГКБ №1");
        fhir.Extension!.Should().Contain(e =>
            e.Url == FhirConstants.SourceEventIdExtensionUrl && e.ValueString == "evt-1");
    }

    // ==== ToCreateCommand ====

    [Fact]
    public void ToCreateCommand_ExtractsAllFields()
    {
        var fhir = NewFhirResource();

        var cmd = PractitionerFhirMapper.ToCreateCommand(fhir, sourceEventId: "evt-1");

        cmd.FirstName.Should().Be("Иван");
        cmd.LastName.Should().Be("Иванов");
        cmd.Phone.Should().Be("+7-900-000-00-00");
        cmd.Email.Should().Be("i@example.ru");
        cmd.Speciality.Should().Be("Кардиолог");
        cmd.PlaceOfWork.Should().Be("ГКБ №1");
        cmd.IsActive.Should().BeTrue();
        cmd.SourceEventId.Should().Be("evt-1");
    }

    [Fact]
    public void ToCreateCommand_WithoutName_ThrowsFhirValidation()
    {
        var fhir = NewFhirResource();
        fhir.Name = null;

        var act = () => PractitionerFhirMapper.ToCreateCommand(fhir, null);

        act.Should().Throw<FhirValidationException>()
            .WithMessage("*name*");
    }

    [Fact]
    public void ToCreateCommand_WithoutEmail_ThrowsFhirValidation()
    {
        var fhir = NewFhirResource();
        fhir.Telecom = new() { new() { System = "phone", Value = "+7-900-000-00-00" } };

        var act = () => PractitionerFhirMapper.ToCreateCommand(fhir, null);

        act.Should().Throw<FhirValidationException>()
            .WithMessage("*email*");
    }

    // ==== ToUpdateCommand ====

    [Fact]
    public void ToUpdateCommand_PassesExpectedVersion()
    {
        var id = Guid.NewGuid();
        var fhir = NewFhirResource();

        var cmd = PractitionerFhirMapper.ToUpdateCommand(id, fhir, expectedVersion: 5);

        cmd.Id.Should().Be(id);
        cmd.ExpectedVersion.Should().Be(5);
        cmd.Email.Should().Be("i@example.ru");
    }

    // ==== ExtractSourceEventId ====

    [Fact]
    public void ExtractSourceEventId_ReturnsValueFromExtension()
    {
        var fhir = NewFhirResource();
        fhir.Extension!.Add(new() { Url = FhirConstants.SourceEventIdExtensionUrl, ValueString = "evt-42" });

        var result = PractitionerFhirMapper.ExtractSourceEventId(fhir);

        result.Should().Be("evt-42");
    }

    // ==== ToBundle ====

    [Fact]
    public void ToBundle_MiddlePage_BuildsSelfPrevAndNext()
    {
        var page = new PagedResult<PractitionerReadModel>(new[] { NewReadModel() }, Total: 50, Offset: 20, Count: 10);

        var bundle = PractitionerFhirMapper.ToBundle(page, new(Count: 10, Offset: 20), "http://test/Practitioner");

        bundle.Type.Should().Be("searchset");
        bundle.Total.Should().Be(50);
        bundle.Entry!.Should().HaveCount(1);
        bundle.Link!.Should().ContainSingle(l => l.Relation == "self");
        bundle.Link!.Should().ContainSingle(l => l.Relation == "previous"); 
        bundle.Link!.Should().ContainSingle(l => l.Relation == "next");   
    }

    [Fact]
    public void ToBundle_FirstPageWithoutPrevious_HasSelfAndNext()
    {
        var page = new PagedResult<PractitionerReadModel>(new[] { NewReadModel() }, Total: 50, Offset: 0, Count: 10);

        var bundle = PractitionerFhirMapper.ToBundle(page, new(Count: 10, Offset: 0), "http://test/Practitioner");

        bundle.Link!.Should().ContainSingle(l => l.Relation == "self");
        bundle.Link!.Should().ContainSingle(l => l.Relation == "next"); 
        bundle.Link!.Should().NotContain(l => l.Relation == "previous"); 
    }
}