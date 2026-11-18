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

public sealed class PractitionerQueryHandlersTests
{
    private static readonly DateTime _now = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IPractitionerRepository _repo = Substitute.For<IPractitionerRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly IPractitionerMetrics _metrics = Substitute.For<IPractitionerMetrics>();

    public PractitionerQueryHandlersTests()
    {
        _clock.UtcNow.Returns(_now.AddHours(1));
    }

    // ==== GetPractitionerByIdQueryHandler ====
    [Fact]
    public async Task GetByIdCacheMissLoadsFromRepoAndCaches()
    {
        var p = NewPractitioner();
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);
        _cache.GetAsync<PractitionerReadModel>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PractitionerReadModel?)null);

        var handler = new GetPractitionerByIdQueryHandler(_repo, _cache);
        var result = await handler.Handle(new GetPractitionerByIdQuery(p.Id), default);

        result.Id.Should().Be(p.Id);
        result.Email.Should().Be("i@example.ru");

        await _repo.Received(1).GetByIdAsync(p.Id, Arg.Any<CancellationToken>());
        await _cache.Received(1).SetAsync(
            Arg.Is<string>(k => k.Contains(p.Id.ToString())),
            Arg.Any<PractitionerReadModel>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdCacheHitDoesNotQueryRepo()
    {
        var id = Guid.NewGuid();
        var cached = new PractitionerReadModel(id, "Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", "i@example.ru", true, _now, _now, 1, null);

        _cache.GetAsync<PractitionerReadModel>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(cached);

        var handler = new GetPractitionerByIdQueryHandler(_repo, _cache);
        var result = await handler.Handle(new GetPractitionerByIdQuery(id), default);

        result.Should().Be(cached);
        await _repo.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ==== SearchPractitionersQueryHandler ====
    [Fact]
    public async Task SearchClampsCountToMaxAndOffsetToNonNegative()
    {
        _repo.SearchAsync(Arg.Any<PractitionerFilter>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<Practitioner>(Array.Empty<Practitioner>(), 0, 0, 100));

        var handler = new SearchPractitionersQueryHandler(_repo);
        await handler.Handle(new SearchPractitionersQuery(new PractitionerFilter(Count: 5000, Offset: -10)), default);

        await _repo.Received(1).SearchAsync(Arg.Is<PractitionerFilter>(f => f.Count == 100 && f.Offset == 0), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SearchZeroCountUsesDefault()
    {
        _repo.SearchAsync(Arg.Any<PractitionerFilter>(), Arg.Any<CancellationToken>()).Returns(new PagedResult<Practitioner>(Array.Empty<Practitioner>(), 0, 0, 20));

        var handler = new SearchPractitionersQueryHandler(_repo);
        await handler.Handle(new SearchPractitionersQuery(new PractitionerFilter(Count: 0)), default);

        await _repo.Received(1).SearchAsync(Arg.Is<PractitionerFilter>(f => f.Count == 20), Arg.Any<CancellationToken>());
    }

    // ==== DeletePractitionerCommandHandler ====
    [Fact]
    public async Task DeleteSuccessMarksDeletedAndInvalidatesCache()
    {
        var p = NewPractitioner();
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var handler = new DeletePractitionerCommandHandler(_repo, _uow, _clock, _cache, _metrics, NullLogger<DeletePractitionerCommandHandler>.Instance);

        await handler.Handle(new DeletePractitionerCommand(p.Id), default);

        p.IsDeleted.Should().BeTrue();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _cache.Received(1).RemoveAsync(Arg.Is<string>(k => k.Contains(p.Id.ToString())), Arg.Any<CancellationToken>());
        _metrics.Received(1).CacheInvalidationSucceeded();
        _metrics.DidNotReceive().CacheInvalidationFailed();
    }

    [Fact]
    public async Task DeleteCacheInvalidationFailsStillSucceedsAndTracksMetric()
    {
        var p = NewPractitioner();
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        _cache.When(c => c.RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())).Do(_ => throw new InvalidOperationException("Redis down"));

        var handler = new DeletePractitionerCommandHandler(_repo, _uow, _clock, _cache, _metrics, NullLogger<DeletePractitionerCommandHandler>.Instance);

        // Should not throw — cache failure is tolerated
        await handler.Handle(new DeletePractitionerCommand(p.Id), default);

        p.IsDeleted.Should().BeTrue();
        _metrics.Received(1).CacheInvalidationFailed();
        _metrics.DidNotReceive().CacheInvalidationSucceeded();
    }

    [Fact]
    public async Task DeleteAlreadyDeletedThrowsGoneException()
    {
        var p = NewPractitioner();
        p.MarkAsDeleted(_now);
        _repo.GetByIdAsync(p.Id, Arg.Any<CancellationToken>()).Returns(p);

        var handler = new DeletePractitionerCommandHandler(_repo, _uow, _clock, _cache, _metrics, NullLogger<DeletePractitionerCommandHandler>.Instance);

        var act = () => handler.Handle(new DeletePractitionerCommand(p.Id), default);

        await act.Should().ThrowAsync<GoneException>();
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteNotFoundThrowsNotFoundException()
    {
        _repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((Practitioner?)null);

        var handler = new DeletePractitionerCommandHandler(_repo, _uow, _clock, _cache, _metrics, NullLogger<DeletePractitionerCommandHandler>.Instance);

        var act = () => handler.Handle(new DeletePractitionerCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static Practitioner NewPractitioner(string email = "i@example.ru") => Practitioner.Create("Иван", "Иванов", "+7-900-000-00-00", "Кардиолог", "ГКБ №1", email, true, _now);
}
