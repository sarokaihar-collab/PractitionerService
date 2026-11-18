using FluentAssertions;
using PractitionerService.Api.Fhir;
using PractitionerService.Api.Fhir.Models;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Exceptions;
using PractitionerService.Application.Practitioners;

namespace PractitionerService.UnitTests.Api;

public sealed class PractitionerFhirMapperTests
{
    private static readonly DateTime _now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ToFhirMapsAllFields()
    {
        var rm = NewReadModel();

        var fhir = PractitionerFhirMapper.ToFhir(rm);

        fhir.ResourceType.Should().Be("Practitioner");
        fhir.Id.Should().Be(rm.Id.ToString());
        fhir.Active.Should().BeTrue();
        fhir.Meta!.VersionId.Should().Be("3");
        fhir.Meta.LastUpdated.Should().Be(rm.UpdatedAt);
        fhir.Name!.Single().Family.Should().Be("Иванов");
        fhir.Name!.Single().Given!.Single().Should().Be("Иван");
        fhir.Telecom!.Should().Contain(t => t.System == FhirConstants.TelecomSystemPhone && t.Value == "+7-900-000-00-00");
        fhir.Telecom.Should().Contain(t => t.System == FhirConstants.TelecomSystemEmail && t.Value == "i@example.ru");
        fhir.Contained!.Single().Name.Should().Be("ГКБ №1");
        fhir.Qualification!.Single().Issuer!.Reference.Should().Be("#place-of-work");
        fhir.Meta.Tag!.Should().Contain(t => t.System == FhirConstants.MetaTagSystemCreatedAt);
        fhir.Meta.Tag.Should().Contain(t => t.System == FhirConstants.MetaTagSystemSourceEventId && t.Code == "evt-1");
    }

    [Fact]
    public void ToCreateCommandExtractsAllFields()
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
    public void ToCreateCommandWithoutNameThrowsFhirValidation()
    {
        var fhir = NewFhirResource();
        fhir.Name = null;

        var act = () => PractitionerFhirMapper.ToCreateCommand(fhir, null);

        act.Should().Throw<FhirValidationException>().WithMessage("*name*");
    }

    [Fact]
    public void ToCreateCommandWithoutEmailThrowsFhirValidation()
    {
        var fhir = NewFhirResource();
        fhir.Telecom = [new() { System = "phone", Value = "+7-900-000-00-00" }];

        var act = () => PractitionerFhirMapper.ToCreateCommand(fhir, null);

        act.Should().Throw<FhirValidationException>().WithMessage("*email*");
    }

    [Fact]
    public void ToUpdateCommandPassesExpectedVersion()
    {
        var id = Guid.NewGuid();
        var fhir = NewFhirResource();

        var cmd = PractitionerFhirMapper.ToUpdateCommand(id, fhir, expectedVersion: 5);

        cmd.Id.Should().Be(id);
        cmd.ExpectedVersion.Should().Be(5);
        cmd.Email.Should().Be("i@example.ru");
    }

    [Fact]
    public void ExtractSourceEventIdReturnsValueFromMetaTag()
    {
        var fhir = NewFhirResource();
        fhir.Meta = new()
        {
            Tag =
            [
                new()
                {
                    System = FhirConstants.MetaTagSystemSourceEventId,
                    Code = "evt-42",
                },
            ],
        };

        var result = PractitionerFhirMapper.ExtractSourceEventId(fhir);

        result.Should().Be("evt-42");
    }

    [Fact]
    public void ToBundleMiddlePageBuildsSelfPrevAndNext()
    {
        var page = new PagedResult<PractitionerReadModel>(new[] { NewReadModel() }, Total: 50, Offset: 20, Count: 10);

        var bundle = PractitionerFhirMapper.ToBundle(page, new(Count: 10, Offset: 20), "http://test/Practitioner");

        bundle.Type.Should().Be(FhirConstants.BundleTypeSearchset);
        bundle.Total.Should().Be(50);
        bundle.Entry!.Should().HaveCount(1);
        bundle.Link!.Should().ContainSingle(l => l.Relation == FhirConstants.LinkRelationSelf);
        bundle.Link.Should().ContainSingle(l => l.Relation == FhirConstants.LinkRelationPrevious);
        bundle.Link.Should().ContainSingle(l => l.Relation == FhirConstants.LinkRelationNext);
    }

    [Fact]
    public void ToBundleFirstPageWithoutPreviousHasSelfAndNext()
    {
        var page = new PagedResult<PractitionerReadModel>(new[] { NewReadModel() }, Total: 50, Offset: 0, Count: 10);

        var bundle = PractitionerFhirMapper.ToBundle(page, new(Count: 10, Offset: 0), "http://test/Practitioner");

        bundle.Link!.Should().ContainSingle(l => l.Relation == FhirConstants.LinkRelationSelf);
        bundle.Link.Should().ContainSingle(l => l.Relation == FhirConstants.LinkRelationNext);
        bundle.Link.Should().NotContain(l => l.Relation == FhirConstants.LinkRelationPrevious);
    }

    [Fact]
    public void ToCreateCommandWithoutContainedOrganizationThrowsFhirValidation()
    {
        var fhir = NewFhirResource();
        fhir.Contained = null;

        var act = () => PractitionerFhirMapper.ToCreateCommand(fhir, null);

        act.Should().Throw<FhirValidationException>().WithMessage("*contained Organization*");
    }

    private static PractitionerReadModel NewReadModel() => new(Guid.NewGuid(), "Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", "i@example.ru", true, _now, _now, 3, "evt-1");

    private static FhirPractitionerResource NewFhirResource() => new()
    {
        ResourceType = "Practitioner",
        Active = true,
        Contained =
        [
            new() { Id = FhirConstants.ContainedOrganizationId, Name = "ГКБ №1" },
        ],
        Name = [new() { Family = "Иванов", Given = ["Иван"] }],
        Telecom =
        [
            new() { System = "phone", Value = "+7-900-000-00-00" },
            new() { System = "email", Value = "i@example.ru" },
        ],
        Qualification =
        [
            new()
            {
                Code = new() { Text = "Кардиолог" },
                Issuer = new() { Reference = $"#{FhirConstants.ContainedOrganizationId}" },
            },
        ],
    };
}
