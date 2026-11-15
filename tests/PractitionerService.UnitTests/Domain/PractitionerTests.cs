using FluentAssertions;
using PractitionerService.Domain.Common;
using PractitionerService.Domain.Common.Exceptions;
using PractitionerService.Domain.Practitioners;
using Xunit;

namespace PractitionerService.UnitTests.Domain;

public sealed class PractitionerTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateWithValidDataNormalizesEmailAndSetsVersionToOne()
    {
        var p = Practitioner.Create("Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", "I.IVANOV@CLINIC.RU", true, Now);

        p.Email.Should().Be("i.ivanov@clinic.ru");
        p.Version.Should().Be(1);
        p.IsDeleted.Should().BeFalse();
        p.CreatedAt.Should().Be(Now);
        p.UpdatedAt.Should().Be(Now);
    }

    [Fact]
    public void CreateWithEmptyFirstNameThrowsDomainException()
    {
        var act = () => Practitioner.Create(string.Empty, "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", "i@example.ru", true, Now);

        act.Should().Throw<DomainException>().WithMessage("*firstName*");
    }

    [Fact]
    public void UpdateIncrementsVersionAndSetsUpdatedAt()
    {
        var p = NewPractitioner();
        var later = Now.AddHours(1);

        p.Update("Пётр", "Петров", "+7-900-000-00-01", "Терапевт", "ГКБ №2", "P@EXAMPLE.RU", false, later);

        p.Version.Should().Be(2);
        p.UpdatedAt.Should().Be(later);
        p.FirstName.Should().Be("Пётр");
        p.Email.Should().Be("p@example.ru");
    }

    [Fact]
    public void MarkAsDeletedIsIdempotentAndBlocksSubsequentUpdate()
    {
        var p = NewPractitioner();

        p.MarkAsDeleted(Now.AddMinutes(1));
        p.MarkAsDeleted(Now.AddMinutes(5));
        p.IsDeleted.Should().BeTrue();
        p.IsActive.Should().BeFalse();
        p.Version.Should().Be(2);

        var act = () => p.Update("Пётр", "Петров", "+7-900-000-00-01", "Терапевт", "ГКБ №2", "p@example.ru", false, Now);
        act.Should().Throw<DomainException>().WithMessage("*удалён*");
    }

    [Fact]
    public void UpdateWithInvalidEmailThrowsDomainException()
    {
        var p = NewPractitioner();

        var act = () => p.Update("Пётр", "Петров", "+7-900-000-00-01", "Терапевт", "ГКБ №2", "not-an-email", true, Now.AddHours(1));

        act.Should().Throw<DomainException>().WithMessage("*email*");
    }

    [Fact]
    public void MarkAsDeletedSecondCallDoesNotIncrementVersionAgain()
    {
        var p = NewPractitioner();

        p.MarkAsDeleted(Now.AddMinutes(1));
        p.MarkAsDeleted(Now.AddMinutes(2));
        p.MarkAsDeleted(Now.AddMinutes(3));

        p.Version.Should().Be(2);   // 1 (create) + 1 (delete); повторные вызовы — no-op
    }

    [Fact]
    public void CreateNormalizesPhoneToE164Like()
    {
        var p = Practitioner.Create("Иван", "Иванов", " +7 (900) 100-00-01 ", "Кардиолог", "ГКБ №1", "i@example.ru", true, Now);

        p.Phone.Should().Be("+79001000001");
    }

    [Fact]
    public void CreatePhoneWithoutPlusKeepsDigitsOnly()
    {
        var p = Practitioner.Create("Иван", "Иванов", "8-900-100-00-01", "Кардиолог", "ГКБ №1", "i@example.ru", true, Now);

        p.Phone.Should().Be("89001000001");
    }

    [Fact]
    public void CreatePhoneWithoutDigitsThrowsDomainException()
    {
        var act = () => Practitioner.Create("Иван", "Иванов", "нет цифр", "Кардиолог", "ГКБ №1", "i@example.ru", true, Now);

        act.Should().Throw<DomainException>().WithMessage("Поле 'phone' не содержит цифр.");
    }

    private static Practitioner NewPractitioner(string email = "i.ivanov@clinic.ru", string? sourceEventId = null) =>
        Practitioner.Create("Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", email, true, Now, sourceEventId);
}
