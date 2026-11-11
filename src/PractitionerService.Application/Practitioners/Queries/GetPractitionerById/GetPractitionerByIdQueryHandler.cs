using MediatR;

public sealed class GetPractitionerByIdQueryHandler : IRequestHandler<GetPractitionerByIdQuery, PractitionerDto>
{
    private readonly IPractitionerRepository _repo;

    public GetPractitionerByIdQueryHandler(IPractitionerRepository repo) => _repo = repo;

    public async Task<PractitionerDto> Handle(GetPractitionerByIdQuery q, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(q.Id, ct);
        if (p is null) throw new NotFoundException($"Practitioner {q.Id} не найден.");
        if (p.IsDeleted) throw new GoneException($"Practitioner {q.Id} удалён.");
        return p.ToDto();
    }
}