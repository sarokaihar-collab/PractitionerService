using MediatR;

public sealed class UpdatePractitionerCommandHandler : IRequestHandler<UpdatePractitionerCommand, PractitionerDto>
{
    private readonly IPractitionerRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly IDateTimeProvider _clock;

    public UpdatePractitionerCommandHandler(IPractitionerRepository repo, IUnitOfWork uow, IDateTimeProvider clock)
    {
        _repo = repo;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PractitionerDto> Handle(UpdatePractitionerCommand cmd, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(cmd.Id, ct);
        if (p is null) throw new NotFoundException($"Practitioner {cmd.Id} не найден.");
        if (p.IsDeleted) throw new GoneException($"Practitioner {cmd.Id} удалён и не может быть изменён.");

        if (cmd.ExpectedVersion.HasValue && p.Version != cmd.ExpectedVersion.Value)
            throw new ConflictException($"Версия Practitioner {cmd.Id} изменилась (ожидалась {cmd.ExpectedVersion}, текущая {p.Version}).");

        var newIsActive = cmd.IsActive ?? p.IsActive;
        p.Update(cmd.FirstName, cmd.LastName, cmd.Phone, cmd.Speciality, cmd.PlaceOfWork, cmd.Email, newIsActive, _clock.UtcNow);

        // EF Core with IsConcurrencyToken on Version throws DbUpdateConcurrencyException
        // on a race; Infrastructure maps it to ConflictException.
        await _uow.SaveChangesAsync(ct);
        return p.ToDto();
    }
}