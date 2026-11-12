using MediatR;
using PractitionerService.Application.Common;

public sealed class UpdatePractitionerCommandHandler : IRequestHandler<UpdatePractitionerCommand, PractitionerCommandResult>
{
    private readonly IPractitionerRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _clock;
    private readonly ICacheService _cache;

    public UpdatePractitionerCommandHandler(IPractitionerRepository repository, IUnitOfWork unitOfWork, IDateTimeProvider clock, ICacheService cache)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _cache = cache;
    }

    public async Task<PractitionerCommandResult> Handle(UpdatePractitionerCommand cmd, CancellationToken ct)
    {
        var p = await _repository.GetByIdAsync(cmd.Id, ct);
        if (p is null) throw new NotFoundException($"Врач {cmd.Id} не найден.");
        if (p.IsDeleted) throw new GoneException($"Врач {cmd.Id} удалён и не может быть изменён.");

        if (cmd.ExpectedVersion.HasValue && p.Version != cmd.ExpectedVersion.Value)
            throw new ConflictException($"Версия данных Врача {cmd.Id} изменилась (ожидалась {cmd.ExpectedVersion}, текущая {p.Version}).");

        var newIsActive = cmd.IsActive ?? p.IsActive;
        p.Update(cmd.FirstName, cmd.LastName, cmd.Phone, cmd.Speciality, cmd.PlaceOfWork, cmd.Email, newIsActive, _clock.UtcNow);

        // EF Core with IsConcurrencyToken on Version throws DbUpdateConcurrencyException
        // on a race; Infrastructure maps it to ConflictException.
        await _unitOfWork.SaveChangesAsync(ct);

        await _cache.RemoveAsync(CacheKeys.PractitionerById(cmd.Id), ct);

        return new PractitionerCommandResult(p.Id, p.Version);
    }
}