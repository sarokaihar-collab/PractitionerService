using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Diagnostics;
using PractitionerService.Application.Common.Exceptions;
using PractitionerService.Application.Practitioners;
using PractitionerService.Application.Practitioners.Queries;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.UnitTests.Application;

public sealed class PractitionerHandlersTests
{
    private static readonly DateTime _now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IPractitionerRepository _repo = Substitute.For<IPractitionerRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IPractitionerMetrics _metrics = Substitute.For<IPractitionerMetrics>();

    public PractitionerHandlersTests()
    {
        _clock.UtcNow.Returns(_now);
    }

    // ==== CreatePractitionerCommandHandler ====
    [Fact]
    public async Task CreateWithExistingSourceEventIdReturnsExistingWithoutSaving()
    {
        var existing = Practitioner.Create("Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", "i@example.ru", true, _now, "evt-1");

        _repo.FindBySourceEventIdAsync("evt-1", Arg.Any<CancellationToken>()).Returns(existing);

        var handler = new CreatePractitionerCommandHandler(_repo, _uow, _clock, NullLogger<CreatePractitionerCommandHandler>.Instance, _metrics);

        var cmd = new CreatePractitionerCommand("Другой", "Врач", "+7-900-000-00-99", "Хирург", "ГКБ №9", "other@example.ru", true, "evt-1");

        var result = await handler.Handle(cmd, default);

        result.Id.Should().Be(existing.Id);
        await _repo.DidNotReceive().AddAsync(Arg.Any<Practitioner>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        _metrics.DidNotReceive().PractitionerCreated();
    }

    [Fact]
    public async Task CreateWithoutSourceEventIdCreatesNew()
    {
        Practitioner? captured = null;
        _repo
            .When(r => r.AddAsync(Arg.Any<Practitioner>(), Arg.Any<CancellationToken>()))
            .Do(ci => captured = ci.Arg<Practitioner>());

        var handler = new CreatePractitionerCommandHandler(_repo, _uow, _clock, NullLogger<CreatePractitionerCommandHandler>.Instance, _metrics);

        var cmd = new CreatePractitionerCommand("Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", "i@example.ru", null, null);

        var result = await handler.Handle(cmd, default);

        result.Id.Should().NotBe(Guid.Empty);
        result.Version.Should().Be(1);

        captured.Should().NotBeNull();
        captured.IsActive.Should().BeTrue();   // null → true (FHIR default)
        captured.Email.Should().Be("i@example.ru");

        await _repo.Received(1).AddAsync(Arg.Any<Practitioner>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _metrics.Received(1).PractitionerCreated();
    }

    // ==== GetPractitionerByIdQueryHandler ====
    [Fact]
    public async Task GetByIdNotFoundThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Practitioner?)null);

        var handler = new GetPractitionerByIdQueryHandler(_repo, _cache);
        var act = () => handler.Handle(new GetPractitionerByIdQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdDeletedThrowsGoneException()
    {
        var p = Practitioner.Create("Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", "i@example.ru", true, _now);
        p.MarkAsDeleted(_now);
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var handler = new GetPractitionerByIdQueryHandler(_repo, _cache);
        var act = () => handler.Handle(new GetPractitionerByIdQuery(p.Id), default);

        await act.Should().ThrowAsync<GoneException>();
    }

    // ==== UpdatePractitionerCommandHandler ====
    [Fact]
    public async Task UpdateWithWrongExpectedVersionThrowsConflictException()
    {
        var p = Practitioner.Create("Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", "i@example.ru", true, _now);   // version = 1
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var handler = new UpdatePractitionerCommandHandler(_repo, _uow, _clock, _cache, _metrics, NullLogger<UpdatePractitionerCommandHandler>.Instance);

        var cmd = new UpdatePractitionerCommand(p.Id, "Пётр", "Петров", "+7-900-000-00-01", "Терапевт", "ГКБ №2", "p@example.ru", true, ExpectedVersion: 99);

        var act = () => handler.Handle(cmd, default);

        await act.Should().ThrowAsync<ConflictException>();
        _metrics.DidNotReceive().CacheInvalidationSucceeded();
        _metrics.DidNotReceive().CacheInvalidationFailed();
    }
}
