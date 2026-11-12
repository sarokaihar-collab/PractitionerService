using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PractitionerService.Application.Common;
using PractitionerService.Application.Practitioners.Queries;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.UnitTests.Application;

public sealed class PractitionerHandlersTests
{
    private static readonly DateTime Now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IPractitionerRepository _repository = Substitute.For<IPractitionerRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();

    public PractitionerHandlersTests()
    {
        _clock.UtcNow.Returns(Now);
    }

    // ==== CreatePractitionerCommandHandler ====

    [Fact]
    public async Task Create_WithExistingSourceEventId_ReturnsExistingWithoutSaving()
    {
        var existing = Practitioner.Create("Иван", "Иванов", "+7-900-000-00-00",
            "Кардиолог", "ГКБ №1", "i@example.ru", true, Now, "evt-1");

        _repository.FindBySourceEventIdAsync("evt-1", Arg.Any<CancellationToken>()).Returns(existing);

        var handler = new CreatePractitionerCommandHandler(
            _repository, _unitOfWork, _clock, NullLogger<CreatePractitionerCommandHandler>.Instance);
        var cmd = new CreatePractitionerCommand("Другой", "Врач", "+7-900-000-00-99",
            "Хирург", "ГКБ №9", "other@example.ru", true, "evt-1");

        var result = await handler.Handle(cmd, default);

        result.Id.Should().Be(existing.Id);
        await _repository.DidNotReceive().AddAsync(Arg.Any<Practitioner>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_WithoutSourceEventId_CreatesNew()
    {
        Practitioner? captured = null;
        _repository.When(r => r.AddAsync(Arg.Any<Practitioner>(), Arg.Any<CancellationToken>())).Do(ci => captured = ci.Arg<Practitioner>());

        var handler = new CreatePractitionerCommandHandler(_repository, _unitOfWork, _clock, NullLogger<CreatePractitionerCommandHandler>.Instance);
        var cmd = new CreatePractitionerCommand("Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", "i@example.ru", null, null);

        var result = await handler.Handle(cmd, default);

        result.Id.Should().NotBe(Guid.Empty);
        result.Version.Should().Be(1);

        captured.Should().NotBeNull();
        captured!.IsActive.Should().BeTrue();   // null → true (FHIR-дефолт)
        captured.Email.Should().Be("i@example.ru");

        await _repository.Received(1).AddAsync(Arg.Any<Practitioner>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ==== GetPractitionerByIdQueryHandler ====

    [Fact]
    public async Task GetById_NotFound_ThrowsNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Practitioner?)null);
        var handler = new GetPractitionerByIdQueryHandler(_repository, _cache);

        var act = () => handler.Handle(new GetPractitionerByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetById_Deleted_ThrowsGoneException()
    {
        var p = Practitioner.Create("Иван", "Иванов", "+7-900-000-00-00",
            "Кардиолог", "ГКБ №1", "i@example.ru", true, Now);
        p.MarkAsDeleted(Now);
        _repository.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var handler = new GetPractitionerByIdQueryHandler(_repository, _cache);
        var act = () => handler.Handle(new GetPractitionerByIdQuery(p.Id), CancellationToken.None);

        await act.Should().ThrowAsync<GoneException>();
    }

    // ==== UpdatePractitionerCommandHandler ====

    [Fact]
    public async Task Update_WithWrongExpectedVersion_ThrowsConflictException()
    {
        var p = Practitioner.Create("Иван", "Иванов", "+7-900-000-00-00",
            "Кардиолог", "ГКБ №1", "i@example.ru", true, Now);   // version = 1
        _repository.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var handler = new UpdatePractitionerCommandHandler(_repository, _unitOfWork, _clock, _cache);
        var cmd = new UpdatePractitionerCommand(p.Id, "Пётр", "Петров", "+7-900-000-00-01",
            "Терапевт", "ГКБ №2", "p@example.ru", true, ExpectedVersion: 99);

        var act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<ConflictException>();
    }
}