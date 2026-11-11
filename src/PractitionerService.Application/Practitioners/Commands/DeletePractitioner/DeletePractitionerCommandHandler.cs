using MediatR;

public sealed class DeletePractitionerCommandHandler : IRequestHandler<DeletePractitionerCommand, Unit>
{
    private readonly IPractitionerRepository _repository;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public DeletePractitionerCommandHandler(IPractitionerRepository repository, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _repository = repository;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Unit> Handle(DeletePractitionerCommand cmd, CancellationToken ct)
    {
        var p = await _repository.GetByIdAsync(cmd.Id, ct);
        if (p is null) throw new NotFoundException($"Practitioner {cmd.Id} не найден.");
        if (p.IsDeleted) throw new GoneException($"Practitioner {cmd.Id} уже удалён.");

        p.MarkAsDeleted(_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}