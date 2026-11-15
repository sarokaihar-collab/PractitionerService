using MediatR;
using PractitionerService.Application.Common;
using PractitionerService.Application.Common.Exceptions;

namespace PractitionerService.Application.Practitioners;

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

    public async Task<PractitionerCommandResult> Handle(UpdatePractitionerCommand request, CancellationToken cancellationToken)
    {
        var p = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (p is null)
        {
            throw new NotFoundException($"Врач {request.Id} не найден.");
        }

        if (p.IsDeleted)
        {
            throw new GoneException($"Врач {request.Id} удалён и не может быть изменён.");
        }

        if (request.ExpectedVersion.HasValue && p.Version != request.ExpectedVersion.Value)
        {
            throw new ConflictException($"Версия данных Врача {request.Id} изменилась (ожидалась {request.ExpectedVersion}, текущая {p.Version}).");
        }

        var newIsActive = request.IsActive ?? p.IsActive;
        p.Update(request.FirstName, request.LastName, request.Phone, request.Speciality, request.PlaceOfWork, request.Email, newIsActive, _clock.UtcNow);

        // EF Core with IsConcurrencyToken on Version throws DbUpdateConcurrencyException
        // on a race; Infrastructure maps it to ConflictException.
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _cache.RemoveAsync(CacheKeys.PractitionerById(request.Id), cancellationToken).ConfigureAwait(false);

        return new PractitionerCommandResult(p.Id, p.Version);
    }
}
