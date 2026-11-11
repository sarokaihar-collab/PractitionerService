using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PractitionerService.Domain.Practitioners;

namespace PractitionerService.Infrastructure.Persistence;

internal sealed class PractitionerSeeder
{
    private const string SeedFileRelativePath = "SeedData/practitioners.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private readonly PractitionerDbContext _db;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<PractitionerSeeder> _logger;

    public PractitionerSeeder(PractitionerDbContext db, IDateTimeProvider clock, ILogger<PractitionerSeeder> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await _db.Practitioners.AnyAsync(ct))
        {
            _logger.LogInformation("Тестовые данные уже присутствуют, сидинг пропущен.");
            return;
        }

        var filePath = Path.Combine(AppContext.BaseDirectory, SeedFileRelativePath);
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Файл тестовых данных не найден: {filePath}", filePath);

        await using var stream = File.OpenRead(filePath);
        var seed = await JsonSerializer.DeserializeAsync<List<SeedPractitioner>>(stream, JsonOptions, ct)
                   ?? throw new InvalidOperationException($"Не удалось прочитать файл тестовых данных: {filePath}");

        var now = _clock.UtcNow;
        var practitioners = seed
            .Select(s => Practitioner.Create(s.FirstName, s.LastName, s.Phone, s.Speciality, s.PlaceOfWork, s.Email, s.IsActive, now))
            .ToList();

        _db.Practitioners.AddRange(practitioners);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Загружено {Count} тестовых врачей.", practitioners.Count);
    }
}