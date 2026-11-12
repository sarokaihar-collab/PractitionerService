using Microsoft.EntityFrameworkCore;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Infrastructure.Persistence.repositorysitories;

internal sealed class PractitionerRepository : IPractitionerRepository
{
    private readonly PractitionerDbContext _db;

    public PractitionerRepository(PractitionerDbContext db) => _db = db;

    public Task<Practitioner?> GetByIdAsync(Guid id, CancellationToken ct = default) => _db.Practitioners.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Practitioner?> FindBySourceEventIdAsync(string sourceEventId, CancellationToken ct = default) =>
        _db.Practitioners.FirstOrDefaultAsync(p => p.SourceEventId == sourceEventId, ct);

    public async Task<PagedResult<Practitioner>> SearchAsync(PractitionerFilter filter, CancellationToken ct = default)
    {
        IQueryable<Practitioner> query = _db.Practitioners.AsNoTracking();

        if (!filter.IncludeDeleted)
            query = query.Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(filter.NameContains))
        {
            var pattern = $"%{filter.NameContains.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.FirstName, pattern) || EF.Functions.ILike(p.LastName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.FamilyContains))
        {
            var pattern = $"%{filter.FamilyContains.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.LastName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.GivenContains))
        {
            var pattern = $"%{filter.GivenContains.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.FirstName, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.SpecialityContains))
        {
            var pattern = $"%{filter.SpecialityContains.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Speciality, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.PlaceOfWorkContains))
        {
            var pattern = $"%{filter.PlaceOfWorkContains.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.PlaceOfWork, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.EmailEquals))
        {
            var email = filter.EmailEquals.Trim().ToLowerInvariant();
            query = query.Where(p => p.Email == email);
        }

        if (!string.IsNullOrWhiteSpace(filter.PhoneContains))
        {
            var pattern = $"%{filter.PhoneContains.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Phone, pattern));
        }

        if (filter.Active.HasValue)
            query = query.Where(p => p.IsActive == filter.Active.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Skip(filter.Offset)
            .Take(filter.Count)
            .ToListAsync(ct);

        return new PagedResult<Practitioner>(items, total, filter.Offset, filter.Count);
    }

    public async Task AddAsync(Practitioner practitioner, CancellationToken ct = default) =>
        await _db.Practitioners.AddAsync(practitioner, ct);
}