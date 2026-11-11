using MediatR;
using Microsoft.AspNetCore.Mvc;
using PractitionerService.Api.Fhir;
using PractitionerService.Api.Fhir.Models;
using PractitionerService.Api.Controllers.Dtos;

namespace PractitionerService.Api.Controllers;

/// <summary>
/// REST API для работы со справочником врачей. Реализует FHIR R5 Practitioner.
/// </summary>
[ApiController]
[Route("Practitioner")]
[Produces(FhirConstants.MediaTypeFhirJson)]
public sealed class PractitionerController : ControllerBase
{
    private readonly IMediator _mediator;

    public PractitionerController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Создаёт нового врача.
    /// </summary>
    /// <remarks>
    /// Поддерживается идемпотентность через заголовок `Idempotency-Key` или extension
    /// `source-event-id` в теле. Повторный POST с тем же ключом вернёт существующий ресурс.
    /// При конфликте email или телефона — 409 Conflict.
    /// </remarks>
    /// <param name="resource">FHIR-ресурс Practitioner в формате JSON.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <response code="201">Врач создан. Возвращается ресурс с заполненными id и meta.versionId.</response>
    /// <response code="409">Конфликт: email или телефон уже занят другим активным врачом.</response>
    /// <response code="422">Нарушение структуры FHIR или бизнес-инвариантов домена.</response>
    [HttpPost]
    [HttpPost]
    [ProducesResponseType(typeof(FhirPractitionerResource), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] FhirPractitionerResource resource, CancellationToken ct)
    {
        var sourceEventId = PractitionerFhirMapper.ExtractSourceEventId(resource);
        if (string.IsNullOrEmpty(sourceEventId))
        {
            var key = Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(key)) sourceEventId = $"idem:{key}";
        }

        var command = PractitionerFhirMapper.ToCreateCommand(resource, sourceEventId);
        var result = await _mediator.Send(command, ct);
        var readModel = await _mediator.Send(new GetPractitionerByIdQuery(result.Id), ct);

        var location = $"{Request.Scheme}://{Request.Host}/Practitioner/{result.Id}";
        Response.Headers.Location = location;
        Response.Headers.ETag = $"\"{result.Version}\"";

        return StatusCode(StatusCodes.Status201Created, PractitionerFhirMapper.ToFhir(readModel));
    }

    /// <summary>
    /// Возвращает врача по идентификатору.
    /// </summary>
    /// <param name="id">UUID врача.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <response code="200">Врач найден.</response>
    /// <response code="404">Врач с таким id никогда не существовал.</response>
    /// <response code="410">Врач удалён (soft delete).</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FhirPractitionerResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetPractitionerByIdQuery(id), ct);
        Response.Headers.ETag = $"\"{dto.Version}\"";
        return Ok(PractitionerFhirMapper.ToFhir(dto));
    }

    /// <summary>
    /// Полностью обновляет данные врача (PUT).
    /// </summary>
    /// <remarks>
    /// Для защиты от гонок используется заголовок `If-Match: "&lt;version&gt;"`.
    /// Если версия не совпадает — возвращается 409 Conflict. Восстановление удалённых врачей через PUT запрещено (410 Gone).
    /// </remarks>
    /// <param name="id">UUID врача.</param>
    /// <param name="resource">FHIR-ресурс Practitioner — полная новая версия.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <response code="200">Обновлено. ETag содержит новую версию.</response>
    /// <response code="404">Врач не найден.</response>
    /// <response code="409">Конфликт версий или уникальности email/телефона.</response>
    /// <response code="410">Врач удалён.</response>
    /// <response code="422">Нарушение структуры FHIR или бизнес-инвариантов домена.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FhirPractitionerResource), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] FhirPractitionerResource resource, CancellationToken ct)
    {
        int? expectedVersion = null;
        var ifMatch = Request.Headers.IfMatch.FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            var raw = ifMatch.Replace("W/", "", StringComparison.Ordinal).Trim('"', ' ');
            if (int.TryParse(raw, out var v)) expectedVersion = v;
        }

        var command = PractitionerFhirMapper.ToUpdateCommand(id, resource, expectedVersion);
        var result = await _mediator.Send(command, ct);
        var readModel = await _mediator.Send(new GetPractitionerByIdQuery(result.Id), ct);

        Response.Headers.ETag = $"\"{result.Version}\"";
        return Ok(PractitionerFhirMapper.ToFhir(readModel));
    }

    /// <summary>
    /// Удаляет врача (soft delete).
    /// </summary>
    /// <param name="id">UUID врача.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <response code="204">Удалено (или уже было удалено ранее).</response>
    /// <response code="404">Врач не найден.</response>
    /// <response code="410">Врач уже удалён ранее.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(FhirOperationOutcome), StatusCodes.Status410Gone)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeletePractitionerCommand(id), ct);
        return NoContent();
    }

    /// <summary>
    /// Поиск врачей по фильтрам. Возвращает FHIR Bundle (searchset) с stateless-пагинацией.
    /// </summary>
    /// <remarks>
    /// Пагинация: параметры `_count` и `_offset`. В ответе `link[self]`, `link[previous]` (если offset &gt; 0),
    /// `link[next]` (если есть ещё записи). Все ссылки самодостаточны — содержат исходные параметры поиска.
    /// </remarks>
    /// <param name="query">Параметры поиска.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <response code="200">Bundle с найденными врачами и total.</response>
    [HttpGet]
    [ProducesResponseType(typeof(FhirBundle), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] PractitionerSearchQuery query, CancellationToken ct)
    {
        var filter = new PractitionerFilter(
            NameContains: query.Name,
            FamilyContains: query.Family,
            GivenContains: query.Given,
            SpecialityContains: query.Specialty,
            PlaceOfWorkContains: query.PlaceOfWork,
            EmailEquals: query.Email,
            PhoneContains: query.Phone,
            Active: query.Active,
            Offset: query.Offset ?? 0,
            Count: query.Count ?? 20,
            IncludeDeleted: query.IncludeDeleted ?? false);

        var page = await _mediator.Send(new SearchPractitionersQuery(filter), ct);

        var baseUrl = $"{Request.Scheme}://{Request.Host}/Practitioner";
        var normalizedFilter = filter with { Offset = page.Offset, Count = page.Count };

        var bundle = PractitionerFhirMapper.ToBundle(page, normalizedFilter, baseUrl);
        PractitionerFhirMapper.AppendNextIfAny(bundle, normalizedFilter, baseUrl);

        return Ok(bundle);
    }
}